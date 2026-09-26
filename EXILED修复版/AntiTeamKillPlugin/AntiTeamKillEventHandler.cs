using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillEventHandler
    {
        private readonly Dictionary<string, int> _teamKillCount = new Dictionary<string, int>();
        private readonly Dictionary<string, DateTime> _killTime = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, HashSet<string>> _teamKilledVictims = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, string> _killedBy = new Dictionary<string, string>();
        // BUG-07修复: 记录"谁杀了谁"的时间，重生长时间后视为过期，避免用旧数据开庭/变教程
        private readonly Dictionary<string, DateTime> _killedByTime = new Dictionary<string, DateTime>();
        // 击杀记录有效期（秒）：超过该时长后不再作为开庭/变教程依据
        private const int KilledByValidSeconds = 180;

        private Dictionary<string, List<string>> _warnings = new Dictionary<string, List<string>>();

        public readonly List<AdminMessage> AdminMessages = new List<AdminMessage>();
        private static readonly object _msgLock = new object();
        private const int MaxVisibleMessages = 8;
        private const int MessageLifetimeSeconds = 300;

        public class AdminMessage
        {
            public string PlayerName { get; set; }
            public string UserId { get; set; }
            public string Message { get; set; }
            public DateTime Time { get; set; }
        }

        private string DataDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EXILED", AntiTeamKillPlugin.Instance.Config.DataDirectory);

        private string WarningsFile => Path.Combine(DataDir, AntiTeamKillPlugin.Instance.Config.WarningsFile);

        public AntiTeamKillEventHandler()
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);
            LoadAllData();
        }

        // ==================== 组杀检测 ====================

        public void OnHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                if (ev.Attacker.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp)
                {
                    float dmg = ev.Amount;
                    if (dmg <= 0) return;

                    // 攻击队友：扣HP
                    var prop = ev.DamageHandler.GetType().GetProperty("Damage");
                    if (prop != null && prop.CanWrite)
                    {
                        float afterDmg = Math.Max(0, dmg - 1);
                        prop.SetValue(ev.DamageHandler, afterDmg);
                    }

                    DeductXp(ev.Attacker.UserId, AntiTeamKillPlugin.Instance.Config.TeamHitXpPenalty);

                    string adminMsg = $"<size=20><color=red>⚠ {ev.Attacker.Nickname} 攻击了队友 {ev.Player.Nickname} 立即处理!</color></size>";
                    foreach (var admin in Player.List.Where(p => p != null && p.RemoteAdminAccess))
                    {
                        // UI-01修复: 原生 ShowHint 被 HSM 覆盖，改用 HSM 下发
                        ShowAlertHint(admin, adminMsg, 20, 6f);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"组杀检测错误: {ex.Message}"); }
        }

        public void OnDied(DiedEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                // BUG-09修复: UserId 可能为空(NPC/未完成验证玩家)，空键会互相覆盖导致数据错乱
                if (string.IsNullOrEmpty(ev.Player.UserId) || string.IsNullOrEmpty(ev.Attacker.UserId)) return;

                _killedBy[ev.Player.UserId] = ev.Attacker.UserId;
                _killedByTime[ev.Player.UserId] = DateTime.Now; // BUG-07修复: 记录击杀时间

                if (ev.Attacker.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp && !ev.Attacker.IsScp)
                {
                    string killerId = ev.Attacker.UserId;
                    string victimId = ev.Player.UserId;

                    // 提示受害者："你被队友击杀"，可输入 .tk 发起开庭
                    try
                    {
                        // UI-01修复: 原生 ShowHint 被 HSM 覆盖，受害者的 .tk 引导会看不见，改用 HSM
                        ShowAlertHint(ev.Player,
                            $"<size=22><color=red>⚠ 你被队友 {ev.Attacker.Nickname} 击杀！</color>\n" +
                            $"<size=18><color=white>若掌握充足证据，输入 <color=#FFD700>.tk</color> 发起开庭</color></size></size>",
                            22, 8f);
                    }
                    catch { }

                    if (!_teamKilledVictims.ContainsKey(killerId))
                        _teamKilledVictims[killerId] = new HashSet<string>();
                    _teamKilledVictims[killerId].Add(victimId);

                    int kills = _teamKilledVictims[killerId].Count;

                    // BUG-01修复: 组杀惩罚统一为"扣经验 + 扣积分"，文案数值直接取自配置，避免提示与实际不符
                    DeductXp(killerId, AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty);
                    DeductPoints(killerId, AntiTeamKillPlugin.Instance.Config.TeamKillPointsPenalty);

                    Log.Info($"[反组杀] {ev.Attacker.Nickname} 击杀队友 {ev.Player.Nickname} (本局第{kills}次) → 扣{AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty}XP + {AntiTeamKillPlugin.Instance.Config.TeamKillPointsPenalty}积分");

                    if (kills >= AntiTeamKillPlugin.Instance.Config.MaxTeamKillsPerRound)
                    {
                        PunishTeamKiller(ev.Attacker, kills);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"组杀死亡错误: {ex.Message}"); }
        }

        /// <summary>
        /// BUG-02修复: 延迟执行角色变更。
        /// 在 Died 事件回调中直接 Role.Set 会同步触发 Spawned/RoleChanged 等连锁事件，
        /// 与其他插件（如 ExperiencePlugin 的 OnSpawned 发 buff）形成事件重入，
        /// 可能导致死亡状态异常。这里使用 MEC 的 Timing.CallDelayed 延迟到主线程下一帧执行
        /// （MEC 协程是 Unity 安全的，不会像 System.Timers.Timer 那样跨线程操作游戏对象）。
        /// </summary>
        private static void SetRoleDelayed(Player player, RoleTypeId role, float delaySeconds = 0.1f)
        {
            if (player == null) return;
            MEC.Timing.CallDelayed(delaySeconds, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;
                    player.Role.Set(role, Exiled.API.Enums.SpawnReason.Respawn, RoleSpawnFlags.All);
                }
                catch (Exception ex) { Log.Error($"[反组杀] 延迟设置角色失败: {ex.Message}"); }
            });
        }

        private void PunishTeamKiller(Player killer, int totalKills)
        {
            try
            {
                Log.Info($"[反组杀] {killer.Nickname} 本局组杀{totalKills}次 → 自动处罚为教程角色");
                // BUG-02修复: 延迟设置角色，避免在 Died 事件中同步修改角色造成事件重入
                SetRoleDelayed(killer, RoleTypeId.Tutorial);
                string msg = $"<size=20><color=red>⚠ 玩家 {killer.Nickname} 因组杀{totalKills}次已被处罚为教程角色</color></size>";
                NotifyAdmins(msg, 10);
            }
            catch (Exception ex) { Log.Error($"处罚错误: {ex.Message}"); }
        }

        public string GetKillerUserId(string victimUserId)
        {
            // BUG-07修复: 击杀记录超过有效期视为失效，避免玩家重生后仍能用旧记录开庭/变教程
            if (_killedByTime.TryGetValue(victimUserId, out var t)
                && (DateTime.Now - t).TotalSeconds > KilledByValidSeconds)
            {
                _killedBy.Remove(victimUserId);
                _killedByTime.Remove(victimUserId);
                return null;
            }
            return _killedBy.TryGetValue(victimUserId, out var killerId) ? killerId : null;
        }

        /// <summary>BUG-07修复: 玩家重生时清除其击杀记录，避免复活后仍被当作"刚被击杀"</summary>
        public void OnSpawned(SpawnedEventArgs ev)
        {
            try
            {
                if (ev.Player == null || string.IsNullOrEmpty(ev.Player.UserId)) return;
                _killedBy.Remove(ev.Player.UserId);
                _killedByTime.Remove(ev.Player.UserId);
            }
            catch (Exception ex) { Log.Error($"重生清理错误: {ex.Message}"); }
        }

        // ==================== TK 开庭系统 ====================

        /// <summary>开庭案件</summary>
        public class TkCase
        {
            public string CaseId { get; set; }        // 4位随机案件号
            public string ApplicantId { get; set; }   // 开通者（受害者）
            public string ApplicantName { get; set; }
            public string DefendantId { get; set; }   // 被告（击杀者）
            public string DefendantName { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool Confirmed { get; set; }       // 管理员是否已确认开庭
        }

        // CaseId -> 案件
        private readonly Dictionary<string, TkCase> _tkCases = new Dictionary<string, TkCase>();
        // 玩家UserId -> 当前案件号
        private readonly Dictionary<string, string> _playerTkCase = new Dictionary<string, string>();
        // BUG-08修复: 开庭案件有效期（秒），超时自动作废，避免玩家被永久卡住无法再次发起
        private const int TkCaseTimeoutSeconds = 300;

        /// <summary>BUG-08修复: 清理超时案件，释放玩家发起开庭的资格</summary>
        private void CleanupExpiredTkCases()
        {
            if (_tkCases.Count == 0) return;
            var now = DateTime.Now;
            var expired = new List<string>();
            foreach (var kvp in _tkCases)
            {
                if ((now - kvp.Value.CreatedAt).TotalSeconds > TkCaseTimeoutSeconds)
                    expired.Add(kvp.Key);
            }
            foreach (var id in expired)
            {
                if (_tkCases.TryGetValue(id, out var tk))
                {
                    _playerTkCase.Remove(tk.ApplicantId);
                    _tkCases.Remove(id);
                    Log.Info($"[开庭] 案件号{id} 超时({TkCaseTimeoutSeconds}秒未确认)已自动作废");
                }
            }
        }

        /// <summary>BUG-27修复: 静态共享 Random，避免每次 new Random() 在快速连续调用时生成相同序列</summary>
        private static readonly Random _rng = new Random();

        /// <summary>生成4位随机案件号（不含已存在/易混淆字符）</summary>
        private static string GenerateCaseId()
        {
            const string chars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"; // 去易混淆
            lock (_rng)
            {
                return new string(Enumerable.Repeat(chars, 4).Select(s => s[_rng.Next(s.Length)]).ToArray());
            }
        }

        /// <summary>受害者发起开庭。返回生成或已存在的案件号；若条件不满足返回 null</summary>
        public string StartTkCase(Player applicant)
        {
            // BUG-08修复: 先清理超时案件，避免玩家被作废案件永久占用发起资格
            CleanupExpiredTkCases();

            // BUG-08修复: UserId 为空时直接失败，避免字典传入 null 键抛 ArgumentNullException
            if (applicant == null || string.IsNullOrEmpty(applicant.UserId))
                return null;

            // 已在开庭中则返回原案件号
            if (_playerTkCase.TryGetValue(applicant.UserId, out string existingId))
                return existingId;

            string killerId = GetKillerUserId(applicant.UserId);
            if (killerId == null)
                return null; // 无法找到击杀者

            var defendant = Player.List.FirstOrDefault(p => p != null && p.UserId == killerId);
            string defendantName = defendant != null ? defendant.Nickname : "未知玩家";

            string caseId = GenerateCaseId();
            while (_tkCases.ContainsKey(caseId))
                caseId = GenerateCaseId();

            var tk = new TkCase
            {
                CaseId = caseId,
                ApplicantId = applicant.UserId,
                ApplicantName = applicant.Nickname,
                DefendantId = killerId,
                DefendantName = defendantName,
                CreatedAt = DateTime.Now,
                Confirmed = false
            };
            _tkCases[caseId] = tk;
            _playerTkCase[applicant.UserId] = caseId;

            // 向所有在线管理员发出提示
            string adminMsg = $"<size=20><color=#FFD700>⚖ 有人发起开庭</color></size>\n" +
                $"<color=white>玩家 <color=#FFD700>{applicant.Nickname}</color>(开通者) 申请开庭</color>\n" +
                $"<color=white>玩家 <color=red>{defendantName}</color>(被告) 进行开庭</color>\n" +
                $"<color=#AAAAAA>案件号: <color=#00FF00>{caseId}</color>  管理员输入 <color=#FFD700>.tk yes {caseId}</color> 确认开庭</color>";
            NotifyAdmins(adminMsg, 15);

            Log.Info($"[开庭] {applicant.Nickname} 发起开庭(案件号{caseId})，被告 {defendantName}");

            return caseId;
        }

        /// <summary>管理员确认开庭。成功返回 true（3人变教程角色）</summary>
        public bool ConfirmTkCase(Player admin, string caseId)
        {
            if (!admin.RemoteAdminAccess)
                return false;

            string upperId = caseId.ToUpperInvariant();
            if (!_tkCases.TryGetValue(upperId, out var tk) || tk.Confirmed)
                return false;

            // 找出开通者、被告、确认的管理员
            var applicant = Player.List.FirstOrDefault(p => p != null && p.UserId == tk.ApplicantId);
            var defendant = Player.List.FirstOrDefault(p => p != null && p.UserId == tk.DefendantId);

            tk.Confirmed = true;

            // 3人变为教程角色：开通者 + 被告 + 确认开庭的管理员
            var toTutorial = new List<Player>();
            if (applicant != null) toTutorial.Add(applicant);
            if (defendant != null) toTutorial.Add(defendant);
            if (!toTutorial.Contains(admin)) toTutorial.Add(admin);

            foreach (var p in toTutorial)
            {
                // BUG-02修复: 延迟设置角色，避免在命令回调中同步触发连锁事件
                SetRoleDelayed(p, RoleTypeId.Tutorial);
            }

            string names = string.Join("、", toTutorial.Select(p => p.Nickname));
            Log.Info($"[开庭] 管理员 {admin.Nickname} 确认案件号{upperId} → 开庭完成，{names} 变为教程角色");

            string notify = $"<size=20><color=#FFD700>⚖ 开庭完成</color></size>\n<color=white>案件号 {upperId} 已确认，{names} 变为教程角色</color>";
            NotifyAdmins(notify, 10);

            _playerTkCase.Remove(tk.ApplicantId);
            _tkCases.Remove(upperId);

            return true;
        }

        /// <summary>获取指定玩家的待处理案件（用于验证 .tk yes 时案件有效）</summary>
        public TkCase GetTkCase(string caseId)
        {
            _tkCases.TryGetValue(caseId.ToUpperInvariant(), out var tk);
            return tk;
        }

        public List<Player> GetTutorialPlayers()
        {
            return Player.List.Where(p => p != null && p.Role.Type == RoleTypeId.Tutorial).ToList();
        }

        // ===== UI-01修复: 临时通知统一走 HSM =====
        // 原生 Player.ShowHint 与 ExperiencePlugin 的 HSM 提示系统不兼容：
        // 只要玩家屏幕上有任一激活的 HSM hint（ExperiencePlugin 的状态栏即常驻），
        // HSM 会周期性整体重写 hint 区域，原生 ShowHint 的内容随即被覆盖，玩家/管理员根本看不到。
        // 因此所有临时告警通知统一改用 HSM 下发，并复用同一个 hint Id，
        // 使同一玩家收到新通知时"替换"旧通知而不是重叠堆叠。
        // Y 坐标避让: ac_display=200(管理消息) / anti_tk_alert=400(临时通知) / settle_hint=930(结算)
        private const string AlertHintId = "anti_tk_alert";
        private const int AlertHintY = 400;

        /// <summary>UI-01修复: 通过 HSM 下发一条临时通知（自动到期销毁）</summary>
        private static void ShowAlertHint(Player p, string text, int fontSize, float duration)
        {
            if (p == null || !p.IsConnected) return;
            try
            {
                var hint = new HsmHint
                {
                    Id = AlertHintId,
                    Text = text,
                    FontSize = fontSize,
                    YCoordinate = AlertHintY,
                    Alignment = HintAlignment.Center
                };
                PlayerDisplay.Get(p).ShowHint(hint, duration);
            }
            catch (Exception ex) { Log.Error($"[反组杀] 显示提示失败: {ex.Message}"); }
        }

        public static void NotifyAdmins(string message, ushort duration)
        {
            foreach (var p in Player.List)
            {
                // UI-01修复: 改用 HSM，原生 ShowHint 会被 ExperiencePlugin 的 HSM 提示覆盖
                if (p != null && p.RemoteAdminAccess)
                    ShowAlertHint(p, message, 20, duration);
            }
        }

        // ==================== 经验扣除 ====================

        private void DeductXp(string userId, int amount)
        {
            try
            {
                // 查找 ExperiencePlugin 实例，通过其 DataManager 内存操作扣除经验
                // 避免直接读写 YAML 文件导致的性能问题和数据竞争
                var expPlugin = Exiled.Loader.Loader.GetPlugin("ExperiencePlugin");
                if (expPlugin == null)
                {
                    Log.Warn("[反组杀] 未找到 ExperiencePlugin，无法扣XP");
                    return;
                }

                // 通过反射调用 DataManager 的 GetExperience 和修改经验值
                var dataManager = expPlugin.GetType().GetProperty("DataManager")?.GetValue(expPlugin);
                if (dataManager == null)
                {
                    Log.Warn("[反组杀] 未找到 DataManager，无法扣XP");
                    return;
                }

                var getPlayerData = dataManager.GetType().GetMethod("GetPlayerData");
                if (getPlayerData == null) return;

                var playerData = getPlayerData.Invoke(dataManager, new object[] { userId });
                if (playerData == null) return;

                var expProp = playerData.GetType().GetProperty("Experience");
                if (expProp == null) return;

                int currentExp = (int)expProp.GetValue(playerData);
                int newExp = Math.Max(0, currentExp - amount);
                expProp.SetValue(playerData, newExp);

                if (AntiTeamKillPlugin.Instance.Config.Debug)
                    Log.Debug($"[反组杀] {userId} 扣{amount}XP (剩余{newExp})");
            }
            catch (Exception ex) { Log.Error($"扣XP错误: {ex.Message}"); }
        }

        /// <summary>
        /// BUG-01修复: 扣除积分，与 DeductXp 同样通过 ExperiencePlugin 的 DataManager 内存操作。
        /// 组杀惩罚 = 扣经验 + 扣积分，两者数值均来自配置，保证与提示文案一致。
        /// </summary>
        private void DeductPoints(string userId, float amount)
        {
            if (amount <= 0) return;
            try
            {
                var expPlugin = Exiled.Loader.Loader.GetPlugin("ExperiencePlugin");
                if (expPlugin == null)
                {
                    Log.Warn("[反组杀] 未找到 ExperiencePlugin，无法扣积分");
                    return;
                }

                var dataManager = expPlugin.GetType().GetProperty("DataManager")?.GetValue(expPlugin);
                if (dataManager == null) return;

                var getPlayerData = dataManager.GetType().GetMethod("GetPlayerData");
                if (getPlayerData == null) return;

                var playerData = getPlayerData.Invoke(dataManager, new object[] { userId });
                if (playerData == null) return;

                var pointsProp = playerData.GetType().GetProperty("Points");
                if (pointsProp == null) return;

                float currentPoints = Convert.ToSingle(pointsProp.GetValue(playerData));
                float newPoints = Math.Max(0f, currentPoints - amount);
                pointsProp.SetValue(playerData, newPoints);

                if (AntiTeamKillPlugin.Instance.Config.Debug)
                    Log.Debug($"[反组杀] {userId} 扣{amount}积分 (剩余{newPoints:F1})");
            }
            catch (Exception ex) { Log.Error($"扣积分错误: {ex.Message}"); }
        }

        // ==================== 警告系统 ====================

        /// <summary>BUG-25修复: 返回内部列表引用，避免调用方 Add 到临时列表导致数据丢失</summary>
        public List<string> GetWarnings(string userId)
        {
            // 本轮修复: _saveTimer.Elapsed(线程池线程)会调用 FlushWarningsIfDirty→SaveWarnings
            // 遍历序列化 _warnings，与主线程事件回调中的 Add/Get 写操作并发，
            // Dictionary 非线程安全，可能出现枚举时集合被修改异常或内部结构损坏。
            // 统一用 _warnLock 保护 _warnings 的读写。
            lock (_warnLock)
            {
                if (!_warnings.TryGetValue(userId, out var list))
                {
                    list = new List<string>();
                    _warnings[userId] = list;
                }
                return list;
            }
        }

        public void AddWarning(string userId, string warning)
        {
            lock (_warnLock)
            {
                if (!_warnings.ContainsKey(userId))
                    _warnings[userId] = new List<string>();
                _warnings[userId].Add($"[{DateTime.Now:yyyy-MM-dd HH:mm}] {warning}");
                // BUG-26修复: 延迟保存，避免每次加警告都全量写盘
                _warningsDirty = true;
            }
        }

        public int GetTotalWarnings(string userId)
        {
            lock (_warnLock)
            {
                return _warnings.TryGetValue(userId, out var list) ? list.Count : 0;
            }
        }

        // BUG-26修复: 脏标记，由定时器/回合结束时统一落盘
        private volatile bool _warningsDirty;

        // 本轮修复: 保护 _warnings 跨线程访问的锁（与 _warnings 生命周期一致）
        private readonly object _warnLock = new object();

        /// <summary>BUG-26修复: 若有未保存变更则写盘（供定时器/回合事件调用）</summary>
        public void FlushWarningsIfDirty()
        {
            if (!_warningsDirty) return;
            SaveWarnings();
            _warningsDirty = false;
        }

        private void LoadAllData()
        {
            try
            {
                if (File.Exists(WarningsFile))
                {
                    string yaml = File.ReadAllText(WarningsFile);
                    var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();
                    var data = deserializer.Deserialize<Dictionary<string, List<string>>>(yaml);
                    if (data != null) _warnings = data;
                }
            }
            catch (Exception ex) { Log.Error($"加载警告数据失败: {ex.Message}"); }
        }

        public void SaveAllData() { SaveWarnings(); }

        private void SaveWarnings()
        {
            // 本轮修复: 序列化必须在锁内进行，避免与主线程的 AddWarning 并发枚举字典
            lock (_warnLock)
            {
                try
                {
                    var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                        .Build();
                    string yaml = serializer.Serialize(_warnings);
                    File.WriteAllText(WarningsFile, yaml);
                }
                catch (Exception ex) { Log.Error($"保存警告数据失败: {ex.Message}"); }
            }
        }

        // ==================== 管理消息堆叠显示 ====================

        public void AddAdminMessage(string playerName, string userId, string message)
        {
            lock (_msgLock)
            {
                AdminMessages.Add(new AdminMessage
                {
                    PlayerName = playerName, UserId = userId, Message = message, Time = DateTime.Now
                });
                CleanupOldMessages();
            }
            RefreshAdminDisplay();
        }

        public void RefreshAdminDisplay()
        {
            try
            {
                // BUG-24修复: 无管理员在线时直接跳过，避免每8秒全服遍历发 Hint
                var admins = new List<Player>();
                foreach (var p in Player.List)
                {
                    if (p != null && p.RemoteAdminAccess && !p.IsNPC && p.IsConnected)
                        admins.Add(p);
                }
                if (admins.Count == 0) return;

                string display;
                lock (_msgLock)
                {
                    CleanupOldMessages();
                    if (AdminMessages.Count == 0)
                    {
                        // 没有待处理消息时，用空白内容覆盖 AC 提示层（不影响其它层的持久Hint）
                        var blank = new HsmHint
                        {
                            Id = "ac_display",
                            Text = " ",
                            FontSize = 16,
                            YCoordinate = 200,
                            Alignment = HintAlignment.Center
                        };
                        foreach (var p in admins)
                            PlayerDisplay.Get(p).ShowHint(blank, 1f);
                        return;
                    }

                    var msgParts = new List<string>();
                    foreach (var m in AdminMessages)
                    {
                        msgParts.Add($"<color=#FFD700>[玩家→管理]</color> <color=white>{m.PlayerName}</color>: {m.Message}");
                    }
                    display = $"<size=16>{string.Join("\n", msgParts)}</size>";
                }

                // 使用 HSM 持久Hint 展示，避免原生 ShowHint(8秒) 被 ExperiencePlugin 的 HSM 提示系统
                // 覆盖/短暂显示导致管理员看不到 AC 消息。放在屏幕中部上方(Y=200)独立层，不与其它层重叠。
                var hint = new HsmHint
                {
                    Id = "ac_display",
                    Text = display,
                    FontSize = 16,
                    YCoordinate = 200,
                    Alignment = HintAlignment.Center
                };

                foreach (var p in admins)
                    PlayerDisplay.Get(p).ShowHint(hint, 60f);
            }
            catch (Exception ex) { Log.Error($"管理消息显示错误: {ex.Message}"); }
        }

        private void CleanupOldMessages()
        {
            var cutoff = DateTime.Now.AddSeconds(-MessageLifetimeSeconds);
            AdminMessages.RemoveAll(m => m.Time < cutoff);
            while (AdminMessages.Count > MaxVisibleMessages)
                AdminMessages.RemoveAt(0);
        }

        // ==================== 回合清理 ====================

        public void OnRoundStarted()
        {
            _teamKillCount.Clear();
            _teamKilledVictims.Clear();
            _killTime.Clear();
            _killedBy.Clear();
            _killedByTime.Clear();
            _tkCases.Clear();
            _playerTkCase.Clear();
            Log.Info("[反组杀] 新回合 → 组杀追踪/开庭案件已清空");
        }
    }
}
