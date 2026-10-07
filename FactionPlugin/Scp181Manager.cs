using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// SCP-181 幸运儿（自定义角色，D 级载体）：
    ///   - 30% 概率无视权限卡开启门/控制器
    ///   - 10% 概率拾取物品时翻倍或升级（随机二选一，升级走预设表）
    /// 通过 `sx 181 [玩家]` 刷新。
    /// </summary>
    public static class Scp181Manager
    {
        /// <summary>当前 SCP-181 玩家</summary>
        public static readonly HashSet<string> Members = new HashSet<string>();

        /// <summary>无卡开门概率</summary>
        public const double DoorChance = 0.15;
        /// <summary>物品翻倍/升级概率</summary>
        public const double ItemChance = 0.10;
        /// <summary>免疫伤害概率（用户要求 2026-10-01 11:24）</summary>
        public const double DodgeChance = 0.35;

        public static bool Is181(Player player) => player != null && Members.Contains(player.UserId);

        public static void OnRoundEnded()
        {
            Members.Clear();
        }

        public static void OnPlayerLeft(Player player)
        {
            if (player == null) return;
            Members.Remove(player.UserId);
        }

        // ===== 刷新 =====
        public static void Spawn(Player player)
        {
            if (player == null || !player.IsConnected) return;
            // 特殊角色只在 7779 实例启用（用户要求 2026-10-02）
            if (!FactionPlugin.SpecialRolesEnabled) return;
            if (Members.Contains(player.UserId))
            {
                // player.Broadcast(3, "<color=#FFD700>[SCP-181] 你已经是幸运儿了</color>");
                return;
            }

            Members.Add(player.UserId);

            player.Role.Set(RoleTypeId.ClassD, SpawnReason.ForceClass, RoleSpawnFlags.None);

            Timing.CallDelayed(0.5f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;

                    // 用户要求（2026-10-01 11:13 / 11:27）删除头顶头衔显示
                    player.RankName = null;
                    player.CustomInfo = null;

                    player.ClearBroadcasts();
                    // player.Broadcast(8,
//                         "<color=#FFD700>[SCP-181 幸运儿]</color>\n" +
//                         "<color=#AAAAAA>运气就是实力：</color>\n" +
//                         $"<color=#44FF88>{(int)(DoorChance * 100)}%</color> <color=#AAAAAA>无卡开启任何权限门</color>\n" +
//                         $"<color=#44FF88>{(int)(ItemChance * 100)}%</color> <color=#AAAAAA>拾取物品时翻倍或升级</color>");

                    Log.Info($"[SCP-181] {player.Nickname} 已变身为幸运儿");
                }
                catch (Exception ex) { Log.Error($"[SCP-181] 初始化失败: {ex.Message}"); }
            });
        }

        // ===== 15% 无卡开门 =====
        public static void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            try
            {
                if (ev.Player == null || !Is181(ev.Player)) return;
                if (!ev.IsAllowed && UnityEngine.Random.value < DoorChance)
                {
                    ev.IsAllowed = true;
                    ev.Player.ShowHint("<color=#FFD700>═══ [SCP-181 技能发动] ═══</color>\n<color=#44FF88>幸运降临！无卡开门成功！（15% 概率触发）</color>", 3f);
                    Log.Info($"[SCP-181] {ev.Player.Nickname} 触发了无卡开门（15%）");
                }
            }
            catch { }
        }

        // ===== 收容柜（储物柜/军械柜）15% 无卡开启 =====
        public static void OnInteractingLocker(InteractingLockerEventArgs ev)
        {
            try
            {
                if (ev.Player == null || !Is181(ev.Player)) return;
                if (ev.IsAllowed) return;
                if (UnityEngine.Random.value < DoorChance)
                {
                    ev.IsAllowed = true;
                    ev.Player.ShowHint("<color=#FFD700>═══ [SCP-181 技能发动] ═══</color>\n<color=#44FF88>幸运降临！收容柜幸运开启！（15% 概率触发）</color>", 3f);
                    Log.Info($"[SCP-181] {ev.Player.Nickname} 触发了收容柜开启（15%）");
                }
            }
            catch { }
        }

        // ===== 35% 免疫伤害 =====
        public static void OnHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null || !Is181(ev.Player)) return;
                if (ev.Attacker == null) return;   // 环境伤害不免疫
                if (ev.Player == ev.Attacker) return;
                if (UnityEngine.Random.value >= DodgeChance) return;

                ev.IsAllowed = false;
                ev.Player.ShowHint("<color=#FFD700>═══ [SCP-181 技能发动] ═══</color>\n<color=#44FF88>幸运降临！免疫了所有伤害！（35% 概率触发）</color>", 3f);
                Log.Info($"[SCP-181] {ev.Player.Nickname} 触发了免疫伤害（35%）");
            }
            catch { }
        }

        // ===== 10% 物品翻倍/升级 =====
        public static void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            try
            {
                if (ev.Player == null || !Is181(ev.Player)) return;
                if (ev.Pickup == null) return;
                if (UnityEngine.Random.value >= ItemChance) return;

                var type = ev.Pickup.Type;
                bool isDouble = UnityEngine.Random.value < 0.5;   // 翻倍或升级随机二选一

                Timing.CallDelayed(0.4f, () =>
                {
                    try
                    {
                        if (ev.Player == null || !ev.Player.IsConnected || !ev.Player.IsAlive) return;

                        if (isDouble)
                        {
                            // 翻倍：额外给一个同类物品
                            ev.Player.AddItem(type);
                            ev.Player.ShowHint($"<color=#FFD700>[幸运]</color> 物品翻倍！额外获得 <color=#FFFFFF>{type}</color>", 3f);
                        }
                        else if (UpgradeMap.TryGetValue(type, out var upgraded))
                        {
                            // 升级：移除刚捡的，给升级版
                            var original = ev.Player.Items.FirstOrDefault(i => i.Type == type);
                            if (original != null) ev.Player.RemoveItem(original);
                            ev.Player.AddItem(upgraded);
                            ev.Player.ShowHint($"<color=#FFD700>[幸运]</color> 物品升级！<color=#FFFFFF>{type}</color> → <color=#44FF88>{upgraded}</color>", 3f);
                        }
                        else
                        {
                            // 无升级表 → 退回翻倍
                            ev.Player.AddItem(type);
                            ev.Player.ShowHint($"<color=#FFD700>[幸运]</color> 物品翻倍！额外获得 <color=#FFFFFF>{type}</color>", 3f);
                        }
                    }
                    catch (Exception ex) { Log.Debug($"[SCP-181] 物品翻倍/升级失败: {ex.Message}"); }
                });
            }
            catch { }
        }

        // ===== 物品升级表（低 → 高）=====
        private static readonly Dictionary<ItemType, ItemType> UpgradeMap = new Dictionary<ItemType, ItemType>
        {
            // 钥匙卡
            { ItemType.KeycardJanitor, ItemType.KeycardScientist },
            { ItemType.KeycardScientist, ItemType.KeycardResearchCoordinator },
            { ItemType.KeycardZoneManager, ItemType.KeycardContainmentEngineer },
            { ItemType.KeycardContainmentEngineer, ItemType.KeycardMTFOperative },
            { ItemType.KeycardMTFOperative, ItemType.KeycardMTFCaptain },
            { ItemType.KeycardFacilityManager, ItemType.KeycardO5 },
            { ItemType.KeycardMTFCaptain, ItemType.KeycardO5 },
            { ItemType.KeycardResearchCoordinator, ItemType.KeycardFacilityManager },
            // 武器
            { ItemType.GunCOM15, ItemType.GunCOM18 },
            { ItemType.GunCOM18, ItemType.GunCrossvec },
            { ItemType.GunCrossvec, ItemType.GunFSP9 },
            { ItemType.GunFSP9, ItemType.GunE11SR },
            { ItemType.GunAK, ItemType.GunE11SR },
            { ItemType.GunE11SR, ItemType.GunFRMG0 },
            { ItemType.GunA7, ItemType.GunFRMG0 },
            { ItemType.GunRevolver, ItemType.GunShotgun },
            // 医疗
            { ItemType.Painkillers, ItemType.Medkit },
            { ItemType.Medkit, ItemType.Adrenaline },
            { ItemType.Adrenaline, ItemType.SCP500 },
            // 其他
            { ItemType.Radio, ItemType.Flashlight },
            { ItemType.Coin, ItemType.Flashlight },
        };
    }
}
