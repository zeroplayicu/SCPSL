using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        private int _currentMsgIndex = 0;
        private bool _isShowingOverflow = false;
        private const int MaxVisibleMessages = 5;

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
                if (ev.Attacker == null || ev.Target == null) return;
                if (ev.Attacker == ev.Target) return;

                _killedBy[ev.Target.UserId] = ev.Attacker.UserId;

                if (ev.Attacker.Role.Team == ev.Target.Role.Team && !ev.Target.IsScp && !ev.Attacker.IsScp)
                {
                    string killerId = ev.Attacker.UserId;
                    string victimId = ev.Target.UserId;

                    if (!_teamKilledVictims.ContainsKey(killerId))
                        _teamKilledVictims[killerId] = new HashSet<string>();
                    _teamKilledVictims[killerId].Add(victimId);

                    int kills = _teamKilledVictims[killerId].Count;

                    DeductXp(killerId, AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty);

                    Log.Info($"[反组杀] {ev.Attacker.Nickname} 击杀队友 {ev.Target.Nickname} (本局第{kills}次) → 扣{AntiTeamKillPlugin.Instance.Config.TeamKillXpPenalty}XP");

                    if (kills >= AntiTeamKillPlugin.Instance.Config.MaxTeamKillsPerRound)
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
                string dataFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EXILED", "ExperienceData", "player_data.yml");

                if (!File.Exists(dataFile)) return;

                string yaml = File.ReadAllText(dataFile);
                var dic = _deserializer.Deserialize<Dictionary<string, PlayerDataEntry>>(yaml);
                if (dic == null || !dic.TryGetValue(userId, out var entry)) return;

                entry.Experience = Math.Max(0, entry.Experience - amount);
                string newYaml = _serializer.Serialize(dic);
                File.WriteAllText(dataFile, newYaml);

                if (AntiTeamKillPlugin.Instance.Config.Debug)
                    Log.Debug($"[反组杀] {userId} 扣{amount}XP (剩余{entry.Experience})");
            }
            catch (Exception ex) { Log.Error($"扣XP错误: {ex.Message}"); }
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
            catch (Exception ex) { Log.Error($"加载警告数据失败: {ex.Message}"); }
        }

        public void SaveAllData() { SaveWarnings(); }

        private void SaveWarnings()
        {
            try
            {
                string yaml = _serializer.Serialize(_warnings);
                File.WriteAllText(WarningsFile, yaml);
            }
            catch (Exception ex) { Log.Error($"保存警告数据失败: {ex.Message}"); }
        }

        // ==================== 管理消息轮播 ====================

        public void AddAdminMessage(string playerName, string userId, string message)
        {
            AdminMessages.Add(new AdminMessage
            {
                PlayerName = playerName, UserId = userId, Message = message, Time = DateTime.Now
            });
            ShowNextAdminMessage();
        }

        public void ShowNextAdminMessage()
        {
            try
            {
                int total = AdminMessages.Count;
                if (total == 0) return;

                int unread = total - _currentMsgIndex;

                if (unread <= 0)
                {
                    _currentMsgIndex = total > MaxVisibleMessages ? total - MaxVisibleMessages : 0;
                    unread = total - _currentMsgIndex;
                }

                if (unread > MaxVisibleMessages)
                {
                    if (!_isShowingOverflow)
                    {
                        _isShowingOverflow = true;
                        string summary = $"<size=20><color=#FFD700>[管理消息]</color> 您有 <color=white>{unread}</color> 条新消息待阅</size>";
                        foreach (var p in Player.List)
                        {
                            if (p != null && p.RemoteAdminAccess && !p.IsNpc)
                                p.ShowHint(summary, 8);
                        }
                    }
                    return;
                }

                _isShowingOverflow = false;

                var msgParts = new List<string>();
                for (int i = _currentMsgIndex; i < total; i++)
                {
                    var m = AdminMessages[i];
                    msgParts.Add($"<color=#FFD700>[玩家→管理]</color> <color=white>{m.PlayerName}</color>: {m.Message}");
                }

                string fullMsg = $"<size=16>{string.Join("\n", msgParts)}</size>";
                if (unread > 1)
                    fullMsg += $"\n<size=14><color=#AAAAAA>剩余{unread}条 • 自动轮播中</color></size>";

                foreach (var p in Player.List)
                {
                    if (p != null && p.RemoteAdminAccess && !p.IsNpc)
                        p.ShowHint(fullMsg, 8);
                }

                _currentMsgIndex = total;
            }
            catch (Exception ex) { Log.Error($"管理消息轮播错误: {ex.Message}"); }
        }

        // ==================== 回合清理 ====================

        public void OnRoundStarted()
        {
            _teamKillCount.Clear();
            _teamKilledVictims.Clear();
            _killTime.Clear();
            _killedBy.Clear();
            Log.Info("[反组杀] 新回合 → 组杀追踪已清空");
        }
    }
}
