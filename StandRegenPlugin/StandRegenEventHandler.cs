using System;
using System.Collections.Generic;
using Exiled.API.Features;
using PlayerRoles;
using UnityEngine;

namespace StandRegenPlugin
{
    /// <summary>
    /// SCP 站立回血核心逻辑：
    /// - 通过 Timer 每秒检查所有 SCP 玩家
    /// - 记录每个玩家上次位置与时间，判断是否"站立不动"
    /// - 站立不动达到 5 秒（StandDelaySeconds）后开始回血，每秒 +10 血
    /// - 玩家移动则重置站立计时
    /// </summary>
    public class StandRegenEventHandler
    {
        private readonly StandRegenPlugin _plugin;

        /// <summary>玩家 UserId → 站立计时数据</summary>
        private readonly Dictionary<string, StandData> _standData = new Dictionary<string, StandData>();

        private class StandData
        {
            public Vector3 LastPosition;
            public DateTime LastCheckTime;
            public float StandSeconds;      // 已连续站立秒数
            public DateTime LastRegenTime;  // 上次回血时间
            public bool WasStanding;        // 上次是否在站立状态
        }

        public StandRegenEventHandler(StandRegenPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>Timer 回调：每秒检测所有目标玩家并回血</summary>
        public void Tick()
        {
            try
            {
                var now = DateTime.Now;
                foreach (var player in Player.List)
                {
                    if (player == null || !player.IsConnected || player.IsNPC) continue;
                    if (string.IsNullOrEmpty(player.UserId)) continue;
                    if (player.IsDead) continue; // 死亡不回血

                    // 只对 SCP 生效（默认）；SCP-079 的 Position 恒定不变会被误判为"站立"，
                    // 且其 Health 字段实为 AP/能量，回血会变成无限供电
                    if (_plugin.Config.OnlyScp && !player.IsScp) continue;
                    if (player.Role.Type == PlayerRoles.RoleTypeId.Scp079) continue;

                    ProcessPlayer(player, now);
                }

                // 清理掉线的玩家数据
                Cleanup();
            }
            catch (Exception ex)
            {
                Log.Error($"[站立回血] Tick: {ex.Message}");
            }
        }

        private void ProcessPlayer(Player player, DateTime now)
        {
            string id = player.UserId;
            if (!_standData.TryGetValue(id, out var data))
            {
                data = new StandData
                {
                    LastPosition = player.Position,
                    LastCheckTime = now,
                    LastRegenTime = now,
                    StandSeconds = 0f
                };
                _standData[id] = data;
                return;
            }

            // 计算距上次检查的时间间隔
            float delta = (float)(now - data.LastCheckTime).TotalSeconds;
            if (delta <= 0) delta = 0.1f;
            data.LastCheckTime = now;

            // 判断是否移动（位置变化超过阈值）
            Vector3 cur = player.Position;
            float moved = Vector3.Distance(cur, data.LastPosition);
            data.LastPosition = cur;

            if (moved > _plugin.Config.StandStillThreshold)
            {
                // 移动了 → 重置站立计时
                data.StandSeconds = 0f;
                data.WasStanding = false;
                return;
            }

            // 未移动 → 累积站立时间
            data.StandSeconds += delta;
            bool nowStanding = data.StandSeconds >= _plugin.Config.StandDelaySeconds;

            // 刚开始站立满足延迟（从未站立→开始站立）时，重置回血计时，保证从此刻起开始回血
            if (nowStanding && !data.WasStanding)
            {
                data.LastRegenTime = now;
                data.WasStanding = true;
                if (_plugin.Config.Debug)
                    Log.Debug($"[站立回血] {player.Nickname} 开始站立回血");
            }

            // 站立达到延迟后，每秒回血
            if (nowStanding)
            {
                float regenDelta = (float)(now - data.LastRegenTime).TotalSeconds;
                if (regenDelta >= 1f)
                {
                    int ticks = (int)(regenDelta / 1f);
                    if (ticks < 1) ticks = 1;
                    ApplyRegen(player, ticks);
                    data.LastRegenTime = data.LastRegenTime.AddSeconds(ticks);
                }
            }
        }

        private void ApplyRegen(Player player, int ticks)
        {
            try
            {
                // SCP173 使用独立回血量，其余玩家用默认回血量
                float regenPerSecond = player.Role.Type == RoleTypeId.Scp173
                    ? _plugin.Config.Scp173RegenPerSecond
                    : _plugin.Config.RegenPerSecond;

                float amount = regenPerSecond * ticks;
                if (amount <= 0) return;

                float max = player.MaxHealth;
                float cur = player.Health;
                if (cur >= max) return; // 已满血，无需回血

                float newHealth = Mathf.Min(max, cur + amount);
                float actualGain = newHealth - cur;
                player.Health = newHealth;

                if (actualGain > 0)
                {
                    if (_plugin.Config.ShowHint && player.IsAlive)
                        player.ShowHint($"<color=#00FF88>站立回血 +{(int)actualGain}</color>", 1.5f);
                    if (_plugin.Config.Debug)
                        Log.Debug($"[站立回血] {player.Nickname} +{actualGain} 血 ({newHealth}/{max})");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[站立回血] ApplyRegen: {ex.Message}");
            }
        }

        private void Cleanup()
        {
            try
            {
                var toRemove = new List<string>();
                foreach (var kvp in _standData)
                {
                    var p = Player.Get(kvp.Key);
                    if (p == null || !p.IsConnected || p.IsDead)
                        toRemove.Add(kvp.Key);
                }
                foreach (var id in toRemove)
                    _standData.Remove(id);
            }
            catch { }
        }

        /// <summary>回合结束清空数据</summary>
        public void Clear()
        {
            _standData.Clear();
        }
    }
}
