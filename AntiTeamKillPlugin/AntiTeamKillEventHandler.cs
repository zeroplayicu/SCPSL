using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Events.Handlers;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using PlayerRoles;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillEventHandler : CustomEventsHandler
    {
        // ===== 本局组杀追踪 =====
        private readonly Dictionary<string, int> _teamKillCount = new Dictionary<string, int>();        // UserId -> 击杀队友次数
        private readonly Dictionary<string, DateTime> _killTime = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, HashSet<string>> _teamKilledVictims = new Dictionary<string, HashSet<string>>(); // UserId -> {vicitm UserId 集合}
        private readonly Dictionary<string, string> _killedBy = new Dictionary<string, string>(); // victim UserId -> killer UserId

        // ===== 警告数据 =====
        private Dictionary<string, List<string>> _warnings = new Dictionary<string, List<string>>(); // UserId -> [警告内容]

        // ===== 玩家-管理员沟通 =====
        public readonly List<AdminMessage> AdminMessages = new List<AdminMessage>();

        public class AdminMessage
        {
            public string PlayerName { get; set; }
            public string UserId { get; set; }
            public string Message { get; set; }
            public DateTime Time { get; set; }
        }

        // 数据路径
        private string DataDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LabAPI", AntiTeamKillPlugin.Instance.Config.DataDirectory);

        private string WarningsFile => Path.Combine(DataDir, AntiTeamKillPlugin.Instance.Config.WarningsFile);

        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public AntiTeamKillEventHandler()
        {
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            LoadAllData();
        }

        public void RegisterRoundEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
        }

        public void UnregisterRoundEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
        }

        // ==================== 组杀检测 ====================

        public override void OnPlayerHurt(PlayerHurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                if (ev.Attacker.Faction == ev.Player.Faction && !ev.Player.IsSCP)
                {
                    // 队友伤害 → 扣血1 + 扣经验1
                    float dmg = 0f;
                    var prop = ev.DamageHandler.GetType().GetProperty("Damage");
                    if (prop != null) dmg = (float)prop.GetValue(ev.DamageHandler);
                    if (dmg <= 0) return;

                    // 攻击队友：扣HP
                    var setProp = ev.DamageHandler.GetType().GetProperty("Damage");
                    if (setProp != null && setProp.CanWrite)
                    {
                        float afterDmg = Math.Max(0, dmg - 1);
                        setProp.SetValue(ev.DamageHandler, afterDmg);
                    }

                    // 扣经验（通过直接修改YAML文件）
                    DeductXp(ev.Attacker.UserId, AntiTeamKillPlugin.Instance.Config.TeamHitXpPenalty);

                    // 通知管理员
                    string adminMsg = $"<size=20><color=red>⚠ {ev.Attacker.Nickname} 攻击了队友 {ev.Player.Nickname} 立即处理!</color></size>";
                    foreach (var admin in Player.List.Where(p => p != null && p.RemoteAdminAccess))
                    {
                        admin.ClearBroadcasts();
                        admin.SendBroadcast(adminMsg, 6);
                    }
                }
            }
            catch (Exception ex) { Logger.Error($"组杀检测错误: {ex.Message}"); }
        }

        public override void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;

                // 记录谁杀了谁
                _killedBy[ev.Player.UserId] = ev.Attacker.UserId;

                // 检查是否组杀
                if (ev.Attacker.Faction == ev.Player.Faction && !ev.Player.IsSCP && !ev.Attacker.IsSCP)
                {
                    string killerId = ev.Attacker.UserId;
                    string victimId = ev.Player.UserId;

                    // 记录组杀
                    if (!_teamKilledVictims.ContainsKey(killerId))
                        _teamKilledVictims[killerId] = new HashSet<string>();
                    _teamKilledVictims[killerId].Add(victimId);

                    int kills = _teamKilledVictims[killerId].Count;

                    // 扣经验
                    DeductXp(killerId, AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty);

                    Logger.Info($"[反组杀] {ev.Attacker.Nickname} 击杀队友 {ev.Player.Nickname} (本局第{kills}次) → 扣{AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty}XP");

                    // 检查是否达到阈值
                    if (kills >= AntiTeamKillPlugin.Instance.Config.MaxTeamKillsPerRound)
                    {
                        PunishTeamKiller(ev.Attacker, kills);
                    }
                }
            }
            catch (Exception ex) { Logger.Error($"组杀死亡错误: {ex.Message}"); }
        }

        private void PunishTeamKiller(Player killer, int totalKills)
        {
            try
            {
                Logger.Info($"[反组杀] {killer.Nickname} 本局组杀{totalKills}次 → 自动处罚为教程角色");

                // 变为教程角色
                killer.SetRole(RoleTypeId.Tutorial, RoleChangeReason.Respawn, RoleSpawnFlags.All);

                // 通知所有在线管理员
                string msg = $"<size=20><color=red>⚠ 玩家 {killer.Nickname} 因组杀{totalKills}次已被处罚为教程角色</color></size>";
                NotifyAdmins(msg, 10);
            }
            catch (Exception ex) { Logger.Error($"处罚错误: {ex.Message}"); }
        }

        // ==================== 公开方法 ====================

        /// <summary>
        /// 获取某玩家本局杀了谁（用于.ma判断）
        /// </summary>
        public string GetKillerUserId(string victimUserId)
        {
            return _killedBy.TryGetValue(victimUserId, out var killerId) ? killerId : null;
        }

        /// <summary>
        /// 获取所有教程角色玩家列表
        /// </summary>
        public List<Player> GetTutorialPlayers()
        {
            return Player.List.Where(p => p != null && p.Role == RoleTypeId.Tutorial).ToList();
        }

        /// <summary>
        /// 通知所有管理员
        /// </summary>
        public static void NotifyAdmins(string message, ushort duration)
        {
            foreach (var p in Player.List)
            {
                if (p != null && p.RemoteAdminAccess)
                {
                    p.ClearBroadcasts();
                    p.SendBroadcast(message, duration);
                }
            }
        }

        // ==================== 经验扣除 ====================

        private void DeductXp(string userId, int amount)
        {
            try
            {
                string dataFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LabAPI", "ExperienceData", "player_data.yml");

                if (!File.Exists(dataFile)) return;

                string yaml = File.ReadAllText(dataFile);
                var dic = _deserializer.Deserialize<Dictionary<string, PlayerDataEntry>>(yaml);
                if (dic == null || !dic.TryGetValue(userId, out var entry)) return;

                entry.Experience = Math.Max(0, entry.Experience - amount);
                string newYaml = _serializer.Serialize(dic);
                File.WriteAllText(dataFile, newYaml);

                if (AntiTeamKillPlugin.Instance.Config.Debug)
                    Logger.Debug($"[反组杀] {userId} 扣{amount}XP (剩余{entry.Experience})");
            }
            catch (Exception ex) { Logger.Error($"扣XP错误: {ex.Message}"); }
        }

        private class PlayerDataEntry
        {
            public string UserId { get; set; }
            public string PlayerName { get; set; }
            public int Experience { get; set; }
            public int Level { get; set; }
            public int TotalPlayTimeMinutes { get; set; }
            public int TotalKills { get; set; }
            public int TotalDeaths { get; set; }
            public DateTime LastLoginTime { get; set; }
            public DateTime CreatedTime { get; set; }
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
                    var data = _deserializer.Deserialize<Dictionary<string, List<string>>>(yaml);
                    if (data != null) _warnings = data;
                }
            }
            catch (Exception ex) { Logger.Error($"加载警告数据失败: {ex.Message}"); }
        }

        public void SaveAllData()
        {
            SaveWarnings();
        }

        private void SaveWarnings()
        {
            try
            {
                string yaml = _serializer.Serialize(_warnings);
                File.WriteAllText(WarningsFile, yaml);
            }
            catch (Exception ex) { Logger.Error($"保存警告数据失败: {ex.Message}"); }
        }

        // ==================== 玩家管理消息 ====================

        public void AddAdminMessage(string playerName, string userId, string message)
        {
            AdminMessages.Add(new AdminMessage
            {
                PlayerName = playerName,
                UserId = userId,
                Message = message,
                Time = DateTime.Now
            });
        }

        public void ShowAdminMessagesToAdmins()
        {
            if (AdminMessages.Count == 0) return;
            var latest = AdminMessages.Last();
            string msg = $"<size=18><color=#FFD700>[玩家→管理]</color> <color=white>{latest.PlayerName}</color>: {latest.Message}</size>";
            foreach (var p in Player.List)
            {
                if (p != null && p.RemoteAdminAccess)
                {
                    p.ClearBroadcasts();
                    p.SendBroadcast(msg, 8);
                }
            }
        }

        // ==================== 回合清理 ====================

        private void OnRoundStarted()
        {
            _teamKillCount.Clear();
            _teamKilledVictims.Clear();
            _killTime.Clear();
            _killedBy.Clear();
            // 不清理 _warnings（持久化）
            Logger.Info("[反组杀] 新回合 → 组杀追踪已清空");
        }
    }
}
