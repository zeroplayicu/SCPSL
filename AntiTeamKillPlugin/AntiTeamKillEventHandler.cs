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

        public void OnHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                if (ev.Attacker.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp)
                {
                    if (ev.Amount <= 0) return;

                    // 攻击队友：Hurting 阶段直接改伤害（Hurt 时伤害已结算，事后改 DamageHandler 无效）
                    ev.Amount = Math.Max(0, ev.Amount - 1);

                    DeductXp(ev.Attacker.UserId, AntiTeamKillPlugin.Instance.Config.TeamHitXpPenalty);

                    string adminMsg = $"<size=20><color=red>⚠ {ev.Attacker.Nickname} 攻击了队友 {ev.Player.Nickname} 立即处理!</color></size>";
                    foreach (var admin in Player.List.Where(p => p != null && p.RemoteAdminAccess))
                    {
                        admin.ShowHint(adminMsg, 6);
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

                if (ev.Attacker.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp && !ev.Attacker.IsScp)
                {
                    // 仅记录真正的组杀，供 .tk/.ma 使用（记录所有死亡会让敌方击杀也能开庭，波及无辜）
                    _killedBy[ev.Player.UserId] = ev.Attacker.UserId;

                    string killerId = ev.Attacker.UserId;
                    string victimId = ev.Player.UserId;

                    // 提示受害者："你被队友击杀"，可输入 .tk 发起开庭
                    try
                    {
                        ev.Player.ShowHint($"<size=22><color=red>⚠ 你被队友 {ev.Attacker.Nickname} 击杀！</color>\n<size=18><color=white>若掌握充足证据，输入 <color=#FFD700>.tk</color> 发起开庭</color></size></size>", 8);
                    }
                    catch { }

                    if (!_teamKilledVictims.ContainsKey(killerId))
                        _teamKilledVictims[killerId] = new HashSet<string>();
                    _teamKilledVictims[killerId].Add(victimId);

                    int kills = _teamKilledVictims[killerId].Count;

                    DeductXp(killerId, AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty);

                    Log.Info($"[反组杀] {ev.Attacker.Nickname} 击杀队友 {ev.Player.Nickname} (本局第{kills}次) → 扣{AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty}XP");

                    if (kills == AntiTeamKillPlugin.Instance.Config.MaxTeamKillsPerRound)
                    {
                        PunishTeamKiller(ev.Attacker, kills);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"组杀死亡错误: {ex.Message}"); }
        }

        private void PunishTeamKiller(Player killer, int totalKills)
        {
            try
            {
                Log.Info($"[反组杀] {killer.Nickname} 本局组杀{totalKills}次 → 自动处罚为教程角色");
                killer.Role.Set(RoleTypeId.Tutorial, Exiled.API.Enums.SpawnReason.Respawn, RoleSpawnFlags.All);
                string msg = $"<size=20><color=red>⚠ 玩家 {killer.Nickname} 因组杀{totalKills}次已被处罚为教程角色</color></size>";
                NotifyAdmins(msg, 10);
            }
            catch (Exception ex) { Log.Error($"处罚错误: {ex.Message}"); }
        }

        public string GetKillerUserId(string victimUserId)
        {
            return _killedBy.TryGetValue(victimUserId, out var killerId) ? killerId : null;
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

        /// <summary>生成4位随机案件号（不含已存在/易混淆字符）</summary>
        private static string GenerateCaseId()
        {
            var rng = new Random();
            const string chars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"; // 去易混淆
            return new string(Enumerable.Repeat(chars, 4).Select(s => s[rng.Next(s.Length)]).ToArray());
        }

        /// <summary>受害者发起开庭。返回生成或已存在的案件号；若条件不满足返回 null</summary>
        public string StartTkCase(Player applicant)
        {
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
                try { p.Role.Set(RoleTypeId.Tutorial, Exiled.API.Enums.SpawnReason.Respawn, RoleSpawnFlags.All); }
                catch { }
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

        public static void NotifyAdmins(string message, ushort duration)
        {
            foreach (var p in Player.List)
            {
                if (p != null && p.RemoteAdminAccess)
                    p.ShowHint(message, duration);
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

        // ==================== 警告系统 ====================

        public List<string> GetWarnings(string userId)
        {
            return _warnings.TryGetValue(userId, out var list) ? list : new List<string>();
        }

        public void AddWarning(string userId, string warning)
        {
            if (!_warnings.ContainsKey(userId))
                _warnings[userId] = new List<string>();
            _warnings[userId].Add($"[{DateTime.Now:yyyy-MM-dd HH:mm}] {warning}");
            SaveWarnings();
        }

        public int GetTotalWarnings(string userId)
        {
            return _warnings.TryGetValue(userId, out var list) ? list.Count : 0;
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
                        foreach (var p in Player.List)
                        {
                            if (p != null && p.RemoteAdminAccess && !p.IsNPC)
                            {
                                // HSM 同 Id 不替换：先移除旧实例再发空白，否则旧消息残留
                                var disp = PlayerDisplay.Get(p);
                                disp.RemoveHint("ac_display");
                                disp.ShowHint(blank, 1f);
                            }
                        }
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

                foreach (var p in Player.List)
                {
                    if (p != null && p.RemoteAdminAccess && !p.IsNPC)
                    {
                        // HSM 同 Id 不替换不续期：先移除旧实例，避免消息叠加/重影
                        var disp = PlayerDisplay.Get(p);
                        disp.RemoveHint("ac_display");
                        disp.ShowHint(hint, 60f);
                    }
                }
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
            _tkCases.Clear();
            _playerTkCase.Clear();
            Log.Info("[反组杀] 新回合 → 组杀追踪/开庭案件已清空");
        }
    }
}
