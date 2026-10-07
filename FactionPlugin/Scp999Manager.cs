using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    public static class Scp999Manager
    {
        public static HashSet<string> Scp999Players = new HashSet<string>();
        public static HashSet<string> WaitingPlayers = new HashSet<string>();
        public static DateTime LastHealTime = DateTime.MinValue;
        public static float AuraHealRadius = 8f;

        public static void SpawnScp999(Player player)
        {
            if (player == null || !player.IsConnected) return;
            // 特殊角色只在 7779 实例启用（用户要求 2026-10-02）
            if (!FactionPlugin.SpecialRolesEnabled) return;

            Scp999Players.Add(player.UserId);
            player.Role.Set(RoleTypeId.Tutorial, SpawnReason.ForceClass, RoleSpawnFlags.None);

            Timing.CallDelayed(0.5f, () =>
            {
                if (!player.IsConnected) return;

                // 传送到GR-18房间
                TeleportToLcz(player);

                // 血量3500 + HS护盾500
                player.MaxHealth = 3500;
                player.Health = 3500;
                player.ArtificialHealth = 500;

                // 模型缩小0.5倍
                player.Scale = new Vector3(0.5f, 0.5f, 0.5f);

                // 给黑卡
                player.AddItem(ItemType.KeycardO5);

                // 给重甲
                player.AddItem(ItemType.ArmorHeavy);

                // 只给灯（999 只能拾取灯，持灯期间持续恢复生命）
                player.AddItem(ItemType.Lantern);

                // 给SCP-1509(旧报纸)
                player.AddItem(ItemType.SCP1509);

                player.ClearBroadcasts();
                // player.Broadcast(5, "<color=#FF69B4>[SCP-999] 你复活在GR-18房间 | 血量3500 护盾500 | 可拾取灯和武器(武器攻击=治疗) | 持灯每秒回3血</color>");
                Log.Info($"[SCP-999] {player.Nickname} 已变身为SCP-999(教程角色)，复活在GR-18房间");
            });
        }

        private static void TeleportToLcz(Player player)
        {
            // 复活到 GR-18 房间（通过房间名定位）
            foreach (var room in Room.List)
            {
                if (room == null || room.Position == Vector3.zero) continue;
                string roomName = room.Name ?? string.Empty;
                if (roomName.IndexOf("GR18", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    roomName.IndexOf("GR-18", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    player.Position = room.Position + Vector3.up * 1.5f;
                    return;
                }
            }

            // 备选：传送到随机房间
            foreach (var room in Room.List)
            {
                if (room == null || room.Position == Vector3.zero) continue;
                player.Position = room.Position + Vector3.up * 1.5f;
                return;
            }
        }

        public static bool IsScp999(Player player)
        {
            return player != null && Scp999Players.Contains(player.UserId);
        }

        /// <summary>重置单个玩家的SCP-999状态（恢复体型并移出SCP-999记录）</summary>
        public static void ResetPlayer(Player player)
        {
            if (player == null) return;

            if (Scp999Players.Remove(player.UserId))
                Log.Debug($"[SCP-999] 已移除 {player.Nickname} 的SCP-999状态");

            // 恢复玩家体型到默认大小
            try
            {
                if (player.IsConnected && player.ReferenceHub != null)
                {
                    player.Scale = Vector3.one;
                    Log.Debug($"[SCP-999] 已恢复 {player.Nickname} 的体型");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[SCP-999] 恢复体型失败: {ex.Message}");
            }
        }

        /// <summary>判断物品是否允许 SCP-999 拾取（只允许灯）</summary>
        public static bool IsAllowedItem(ItemType type)
        {
            return type == ItemType.Lantern;
        }

        // ===== 持灯生命恢复 =====

        /// <summary>持灯时每秒恢复的生命值</summary>
        public const float LanternHealPerSecond = 3f;

        /// <summary>离开灯（丢掉/被拿走）后仍继续恢复的宽限秒数</summary>
        public const float LanternHealGraceSeconds = 3f;

        /// <summary>各 999 玩家最后一次持灯的时间（用于宽限判定）</summary>
        private static readonly Dictionary<string, DateTime> _lastLanternHold = new Dictionary<string, DateTime>();

        /// <summary>玩家背包/手中是否持有灯</summary>
        public static bool HasLantern(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected) return false;
                foreach (var item in player.Items)
                {
                    if (item != null && item.Type == ItemType.Lantern) return true;
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// 每秒由 FactionPlugin.CooldownRoutine 调用：
        /// SCP-999 持灯期间每秒恢复 3 点生命；离开灯后 3 秒内仍持续恢复（宽限期）。
        /// 只要一直持有灯，恢复就一直持续。
        /// </summary>
        public static void UpdateLanternHeal()
        {
            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive) continue;
                if (!IsScp999(player)) continue;

                string uid = player.UserId;
                if (HasLantern(player))
                {
                    _lastLanternHold[uid] = DateTime.Now;
                    if (player.Health < player.MaxHealth)
                        player.Heal(LanternHealPerSecond);
                }
                else if (_lastLanternHold.TryGetValue(uid, out DateTime last) &&
                         (DateTime.Now - last).TotalSeconds <= LanternHealGraceSeconds)
                {
                    // 离开灯不到 3 秒：仍继续恢复
                    if (player.Health < player.MaxHealth)
                        player.Heal(LanternHealPerSecond);
                }
            }
        }

        public static bool AddToWaiting(Player player)
        {
            if (player == null || !player.IsConnected || IsScp999(player)) return false;
            if (WaitingPlayers.Add(player.UserId))
            {
                player.ShowHint("<color=#FF69B4>[SCP-999] 你已加入SCP-999自选队列，开局将优先变身为SCP-999</color>", 3f);
                return true;
            }
            player.ShowHint("<color=#FF69B4>[SCP-999] 你已在自选队列中</color>", 2f);
            return false;
        }

        public static bool TryAutoSpawn()
        {
            // 服务器人数必须大于15人才会刷新
            if (Player.List.Count <= 15)
            {
                Log.Info($"[SCP-999] 开局自动刷新跳过：在线人数 {Player.List.Count} 未超过15");
                return false;
            }

            // 已有活跃SCP-999则不重复刷新
            foreach (var userId in Scp999Players)
            {
                var p = Player.Get(userId);
                if (p != null && p.IsConnected && p.IsAlive) return false;
            }

            // 优先从自选队列中挑选
            Player selected = null;
            var waitList = WaitingPlayers.ToList();
            foreach (var userId in waitList)
            {
                var p = Player.Get(userId);
                if (p != null && p.IsConnected && p.IsAlive)
                {
                    selected = p;
                    WaitingPlayers.Remove(userId);
                    break;
                }
            }

            // 无自选者则随机从活着的非SCP玩家中挑选
            if (selected == null)
            {
                var candidates = Player.List
                    .Where(p => p != null && p.IsConnected && p.IsAlive && !p.IsScp && !IsScp999(p))
                    .ToList();
                if (candidates.Count > 0)
                    selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            if (selected == null)
            {
                Log.Info("[SCP-999] 开局自动刷新跳过：无符合条件的玩家");
                return false;
            }

            SpawnScp999(selected);
            Log.Info($"[SCP-999] 开局自动刷新：{selected.Nickname} 变身为SCP-999");
            return true;
        }

        public static void HealNearbyAllies()
        {
            if ((DateTime.UtcNow - LastHealTime).TotalSeconds < FactionPlugin.Instance.Config.Scp999AuraHealInterval)
                return;
            LastHealTime = DateTime.UtcNow;

            foreach (var userId in Scp999Players)
            {
                var scp999 = Player.Get(userId);
                if (scp999 == null || !scp999.IsAlive) continue;

                foreach (var target in Player.List)
                {
                    if (target == null || !target.IsAlive || target == scp999) continue;
                    if (target.IsScp) continue;

                    float dist = Vector3.Distance(scp999.Position, target.Position);
                    if (dist <= AuraHealRadius)
                    {
                        float newHp = Math.Min(target.Health + FactionPlugin.Instance.Config.Scp999AuraHealAmount, target.MaxHealth);
                        target.Health = newHp;
                    }
                }
            }
        }

        public static void Reset()
        {
            Scp999Players.Clear();
            WaitingPlayers.Clear();
            _lastLanternHold.Clear();
            LastHealTime = DateTime.MinValue;
        }
    }
}
