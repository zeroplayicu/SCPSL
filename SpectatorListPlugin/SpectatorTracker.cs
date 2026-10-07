using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;

namespace SpectatorListPlugin
{
    /// <summary>
    /// 追踪"被观战者 → 观战者列表"映射。
    /// 直接使用 EXILED 官方 API：<see cref="Player.CurrentSpectatingPlayers"/>
    /// 返回正在观战某玩家的观战者列表，避免手写反射读游戏底层字段导致失效。
    /// </summary>
    public class SpectatorTracker
    {
        /// <summary>被观战者UserId → 观战者列表（观战者UserId）</summary>
        private readonly Dictionary<string, HashSet<string>> _targetSpectators = new Dictionary<string, HashSet<string>>();

        private readonly object _lock = new object();

        public void HandleChangingSpectatedPlayer(ChangingSpectatedPlayerEventArgs ev)
        {
            try
            {
                // 观战目标变化时全量重建映射，简单可靠
                RebuildFromScan();
            }
            catch { }
        }

        /// <summary>
        /// 扫描重建映射：遍历所有活着的玩家，用 EXILED 官方
        /// <see cref="Player.CurrentSpectatingPlayers"/> 获取正在观战他们的观战者列表。
        /// 定期由定时器调用，处理玩家断开、换目标等所有情况。
        /// </summary>
        public void RebuildFromScan()
        {
            try
            {
                var newMap = new Dictionary<string, HashSet<string>>();

                foreach (var target in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)))
                {
                    var spectating = target.CurrentSpectatingPlayers?.ToList();
                    if (spectating == null || spectating.Count == 0)
                        continue;

                    var set = new HashSet<string>();
                    foreach (var spec in spectating)
                    {
                        if (spec == null || string.IsNullOrEmpty(spec.UserId)) continue;
                        set.Add(spec.UserId);
                    }
                    if (set.Count > 0)
                        newMap[target.UserId] = set;
                }

                lock (_lock)
                {
                    _targetSpectators.Clear();
                    foreach (var kvp in newMap)
                        _targetSpectators[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"[观战追踪] 扫描重建出错: {ex.Message}");
            }
        }

        /// <summary>获取"正在观战指定目标"的观战者 Player 列表</summary>
        public List<Player> GetSpectatorsOf(Player target)
        {
            var result = new List<Player>();
            if (target == null || string.IsNullOrEmpty(target.UserId))
                return result;

            lock (_lock)
            {
                if (!_targetSpectators.TryGetValue(target.UserId, out var spectatorIds))
                    return result;
                foreach (var id in spectatorIds)
                {
                    var p = Player.Get(id);
                    if (p != null && p.Role.Type == RoleTypeId.Spectator)
                        result.Add(p);
                }
            }
            return result;
        }

        public void Clear()
        {
            lock (_lock) { _targetSpectators.Clear(); }
        }
    }
}
