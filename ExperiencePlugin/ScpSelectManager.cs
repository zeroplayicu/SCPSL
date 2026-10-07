using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ExperiencePlugin
{
    /// <summary>
    /// SCP自选系统管理器
    /// 流程：玩家输入.scp 049 → 加入等待队列 → SCP死亡时自动替换
    /// </summary>
    public class ScpSelectManager
    {
        private readonly ExperiencePlugin _plugin;

        /// <summary>支持自选的SCP RoleTypeId</summary>
        public readonly List<RoleTypeId> AllowedRoles = new List<RoleTypeId>();

        /// <summary>玩家ID → 目标SCP角色</summary>
        private readonly Dictionary<string, RoleTypeId> _waitingPlayers = new Dictionary<string, RoleTypeId>();

        /// <summary>已批准的SCP替换队列：SCP角色 → (替换者ID, 被替换者ID)</summary>
        private readonly Dictionary<RoleTypeId, (string ReplacerId, string TargetPlayerId)> _approvedReplacements =
            new Dictionary<RoleTypeId, (string, string)>();

        /// <summary>防重复刷新：SCP角色 → 最近一次处理该角色死亡的时间戳(秒)</summary>
        private readonly Dictionary<RoleTypeId, double> _lastDeathProcessed = new Dictionary<RoleTypeId, double>();

        /// <summary>同一SCP角色死亡后，多久内不再重复处理(秒)，防止重复刷新</summary>
        private const double DeathCooldownSeconds = 3.0;

        public ScpSelectManager(ExperiencePlugin plugin)
        {
            _plugin = plugin;
            LoadAllowedRoles();
        }

        public void LoadAllowedRoles()
        {
            AllowedRoles.Clear();
            var configRoles = _plugin.Config.ScpSelectRoles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var roleName in configRoles)
            {
                if (Enum.TryParse<RoleTypeId>(roleName.Trim(), out var roleId))
                    AllowedRoles.Add(roleId);
                else
                    Log.Warn($"[SCP自选] 无效角色名: {roleName}");
            }
        }

        // ==================== 每日次数计算 ====================

        public int GetDailyMaxSelections(PlayerData data)
        {
            if (data == null) return 1;
            int baseCount = 1 + (data.Level / 5) * 2; // 0级=1, 5级=3, 10级=5...
            if (data.VipLevel >= 2) baseCount += _plugin.Config.ScpSelectSvipBonus;
            else if (data.VipLevel >= 1) baseCount += _plugin.Config.ScpSelectVipBonus;
            return baseCount;
        }

        public void CheckDailyReset(PlayerData data)
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            if (data.ScpSelectDate != today)
            {
                data.ScpSelectUsedToday = 0;
                data.ScpSelectDate = today;
            }
        }

        // ==================== 提交自选请求 ====================

        public (bool Success, string Message) SubmitSelection(Player player, RoleTypeId targetRole)
        {
            try
            {
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return (false, "无法读取玩家数据");

                if (!AllowedRoles.Contains(targetRole))
                    return (false, $"不支持自选 {targetRole}。支持: {string.Join(", ", AllowedRoles.Select(r => r.ToString().Replace("Scp", "")))}");

                CheckDailyReset(data);

                int maxDaily = GetDailyMaxSelections(data);
                if (data.ScpSelectUsedToday >= maxDaily)
                    return (false, $"今日SCP自选次数已用完 ({data.ScpSelectUsedToday}/{maxDaily})");

                if (_waitingPlayers.ContainsKey(player.UserId))
                    return (false, "你已经提交过SCP自选请求，请等待");

                // 扣除次数并加入等待队列
                data.ScpSelectUsedToday++;
                _waitingPlayers[player.UserId] = targetRole;
                _plugin.DataManager.SaveAllData();

                string scpNum = targetRole.ToString().Replace("Scp", "");
                // 注意：HSM 不支持内联 <size>（会显示字面量），字号由 HsmHint.FontSize 控制
                string msg = "<color=#FF4B4B>🔴 SCP自选已提交</color>\n" +
                             $"目标: <color=#FF4B4B>SCP-{scpNum}</color>\n" +
                             "当该SCP死亡时自动替换\n" +
                             $"今日剩余: {maxDaily - data.ScpSelectUsedToday}/{maxDaily}次";

                PlayerDisplay.Get(player).ShowHint(new HsmHint
                {
                    Id = "scp_select",
                    Text = msg,
                    FontSize = 14,
                    YCoordinate = 400,
                    Alignment = HintAlignment.Center
                }, 8f);

                Log.Info($"[SCP自选] {player.Nickname} → {targetRole} (今日{data.ScpSelectUsedToday}/{maxDaily})");
                return (true, $"SCP自选成功！等待 SCP-{scpNum} 死亡后替换");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP自选] SubmitSelection: {ex.Message}");
                return (false, "自选失败");
            }
        }

        // ==================== SCP死亡检测 ====================

        /// <summary>
        /// 当有SCP死亡时，检查是否有玩家在等待该SCP角色
        /// </summary>
        public void OnScpDied(Player deadPlayer, RoleTypeId deadRole)
        {
            try
            {
                if (deadPlayer == null) return;

                // 检查该SCP角色是否在允许列表
                if (!AllowedRoles.Contains(deadRole)) return;

                // 防重复刷新：同一SCP角色死亡后短期内(DeathCooldownSeconds)不再重复处理，
                // 避免 Dying/死亡事件重复触发或替换后的新SCP又立即被当作死亡对象，导致重复替换刷新
                double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (_lastDeathProcessed.TryGetValue(deadRole, out double lastTime) && (now - lastTime) < DeathCooldownSeconds)
                {
                    if (_plugin.Config.Debug)
                        Log.Debug($"[SCP自选] {deadRole} 死亡冷却中，跳过(防止重复刷新)");
                    return;
                }
                _lastDeathProcessed[deadRole] = now;

                // 找等待该角色的玩家（按提交顺序，先提交先得）
                string replacerUserId = null;
                foreach (var kvp in _waitingPlayers)
                {
                    if (kvp.Value == deadRole)
                    {
                        replacerUserId = kvp.Key;
                        break;
                    }
                }

                if (replacerUserId == null) return;

                var replacer = Player.Get(replacerUserId);
                if (replacer == null || !replacer.IsAlive)
                {
                    // 玩家已离开或死亡，清除并退还
                    RefundSelection(replacerUserId, "玩家已不在游戏中");
                    return;
                }

                // 批准替换
                _waitingPlayers.Remove(replacerUserId);
                Log.Info($"[SCP自选] {replacer.Nickname} 即将替换为 {deadRole}");

                // 已在事件主线程中，直接执行替换
                try
                {
                    ReplaceWithScp(replacer, deadRole);
                }
                catch (Exception ex)
                {
                    Log.Error($"[SCP自选] 替换失败: {ex.Message}");
                    RefundSelection(replacerUserId, "替换过程出错");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP自选] OnScpDied: {ex.Message}");
            }
        }

        /// <summary>将玩家替换为SCP角色</summary>
        private void ReplaceWithScp(Player player, RoleTypeId scpRole)
        {
            try
            {
                if (player == null || !player.IsConnected) return;

                // 设置角色（使用 All 确保出生点位正确+血量正常，None 会导致0血崩服）
                player.Role.Set(scpRole, RoleSpawnFlags.All);

                // 中途转换SCP时，游戏可能把玩家错误地放到死亡点/原地/错误区域。
                // 在角色完全生效后，将其显式传送到该 SCP 的正确出生点位。
                Timing.CallDelayed(0.4f, () =>
                {
                    try
                    {
                        if (player == null || !player.IsConnected) return;
                        // 角色未成功切换则跳过
                        if (player.Role.Type != scpRole) return;

                        var spawnLoc = player.Role.RandomSpawnLocation;
                        if (spawnLoc != null && spawnLoc.Position != UnityEngine.Vector3.zero)
                        {
                            player.Position = spawnLoc.Position;
                            if (_plugin.Config.Debug)
                                Log.Debug($"[SCP自选] {player.Nickname} 传送到 {scpRole} 出生点 {spawnLoc.Position}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SCP自选] 传送出生点失败: {ex.Message}");
                    }
                });

                // 通知玩家
                string scpNum = scpRole.ToString().Replace("Scp", "");
                player.ShowHint($"<size=18><color=#FF4B4B>🔴 你已成为 SCP-{scpNum}!</color></size>", 5f);
                player.Broadcast(5, $"<color=#FF4B4B>你已成为 SCP-{scpNum}!</color>");

                Log.Info($"[SCP自选] {player.Nickname} 成功替换为 {scpRole}");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP自选] ReplaceWithScp: {ex.Message}");
                RefundSelection(player.UserId, "角色替换失败");
            }
        }

        // ==================== 退还次数 ====================

        private void RefundSelection(string userId, string reason)
        {
            if (_waitingPlayers.Remove(userId))
            {
                var data = _plugin.DataManager.GetPlayerData(userId);
                if (data != null && data.ScpSelectUsedToday > 0)
                {
                    data.ScpSelectUsedToday--;
                    _plugin.DataManager.SaveAllData();
                }

                var player = Player.Get(userId);
                if (player != null)
                {
                    player.ShowHint($"<color=#FFD700>[SCP自选]</color> {reason}，次数已退还", 5f);
                }
                Log.Info($"[SCP自选] 退还 {userId}: {reason}");
            }
        }

        // ==================== 开局自动分配 ====================

        /// <summary>
        /// 回合开始时自动分配：唯一自选者直接成为目标SCP
        /// 多人争同一SCP则拼运气（游戏随机分配），保留队列等待死亡替换
        /// </summary>
        public void OnRoundStarted()
        {
            try
            {
                if (_waitingPlayers.Count == 0) return;

                // 第一步：已经随机分配到目标SCP的玩家，自动从队列移除
                var alreadyGotIt = new List<string>();
                foreach (var kvp in _waitingPlayers)
                {
                    var player = Player.Get(kvp.Key);
                    if (player != null && player.IsAlive && player.Role.Type == kvp.Value)
                    {
                        alreadyGotIt.Add(kvp.Key);
                        string scpNum = kvp.Value.ToString().Replace("Scp", "");
                        Log.Info($"[SCP自选·开局] {player.Nickname} 随机分到 {kvp.Value}，自动匹配！");
                        player.ClearBroadcasts();
                        player.Broadcast(3, $"<color=#00FF00>🎯 运气不错！你被选为 SCP-{scpNum}</color>");
                    }
                }
                foreach (var id in alreadyGotIt)
                    _waitingPlayers.Remove(id);

                if (_waitingPlayers.Count == 0) return;

                // 第二步：按目标SCP分组，唯一自选者自动替换当前SCP持有者
                var groups = _waitingPlayers.GroupBy(kvp => kvp.Value).ToList();

                foreach (var group in groups)
                {
                    var targetRole = group.Key;
                    var waiters = group.Select(g => g.Key).ToList();

                    if (waiters.Count == 1)
                    {
                        string waiterId = waiters[0];
                        var waiter = Player.Get(waiterId);
                        if (waiter == null || !waiter.IsAlive)
                        {
                            RefundSelection(waiterId, "不在游戏中");
                            continue;
                        }
                        if (waiter.Role.Type == targetRole) continue;

                        // 查找当前持有该SCP角色的玩家（非等待者本人）
                        var currentScp = Player.List.FirstOrDefault(p =>
                            p.IsAlive && p.Role.Type == targetRole && p.UserId != waiterId);

                        if (currentScp != null)
                        {
                            _waitingPlayers.Remove(waiterId);
                            string scpNum = targetRole.ToString().Replace("Scp", "");
                            Log.Info($"[SCP自选·开局] {waiter.Nickname} 唯一自选 {targetRole}，自动分配！");

                            // 已在事件主线程中，直接执行替换
                            try
                            {
                                ReplaceWithScp(waiter, targetRole);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[SCP自选·开局] 替换失败: {ex.Message}");
                                RefundSelection(waiterId, "开局自动分配失败");
                            }
                        }
                        else
                        {
                            // 该SCP角色未在游戏中生成（可能人数不够），退还次数
                            RefundSelection(waiterId, $"SCP-{targetRole.ToString().Replace("Scp", "")} 在本局未生成");
                        }
                    }
                    // waiters.Count >= 2 → 多人争同一SCP → 拼运气，保留队列等待死亡替换
                }

                if (_waitingPlayers.Count > 0)
                    Log.Info($"[SCP自选] 剩余 {_waitingPlayers.Count} 人在等待队列中");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP自选·开局] OnRoundStarted: {ex.Message}");
            }
        }

        // ==================== 回合重置 ====================

        public void OnRoundEnd()
        {
            // 回合结束，退还所有未使用的请求
            var userIds = _waitingPlayers.Keys.ToList();
            foreach (var uid in userIds)
                RefundSelection(uid, "回合结束，请求已过期");
            _waitingPlayers.Clear();
            _approvedReplacements.Clear();
            _lastDeathProcessed.Clear(); // 清除冷却，避免影响下回合
        }

        public void OnPlayerLeft(Player player)
        {
            if (player == null) return;
            _waitingPlayers.Remove(player.UserId);
        }

        // ==================== 信息查询 ====================

        public string GetRemainingInfo(PlayerData data)
        {
            if (data == null) return "";
            CheckDailyReset(data);
            int maxDaily = GetDailyMaxSelections(data);
            return $"SCP自选剩余: {maxDaily - data.ScpSelectUsedToday}/{maxDaily}次";
        }

        public int WaitingCount => _waitingPlayers.Count;

        /// <summary>获取当前所有等待中的请求（用于调试）</summary>
        public IReadOnlyDictionary<string, RoleTypeId> WaitingPlayers => _waitingPlayers;
    }
}
