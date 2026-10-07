using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace SCPplaus
{
    /// <summary>
    /// 小僵尸（SCP-049-2）感染技能：
    ///   - 049-2 出生时注册按键「感染」（默认 5 键）
    ///   - 每个小僵尸只能使用一次：找到一名观战玩家，将其变为 049-2（200 血）
    ///   - 被「小僵尸感染」的僵尸固定 200 血（与 049 直接拉起的 750 区分）
    /// ID 范围：24001-24009（7778 实例专用，与 7779 的 FactionPlugin 无冲突）
    /// </summary>
    public static class ZombieInfectionManager
    {
        private const int IdHeader = 24001;
        private const int IdInfect = 24002;

        /// <summary>小僵尸已使用过感染技能的标记</summary>
        private static readonly HashSet<string> UsedInfection = new HashSet<string>();

        /// <summary>被小僵尸感染的玩家（出生时血量固定 200）</summary>
        private static readonly HashSet<string> InfectedByZombie = new HashSet<string>();

        /// <summary>049-2 的 SSS 设置实例（出生时注册、死亡/离开时注销）</summary>
        private static readonly Dictionary<string, List<SettingBase>> PlayerSettings = new Dictionary<string, List<SettingBase>>();

        public static bool IsZombieInfected(string userId) => !string.IsNullOrEmpty(userId) && InfectedByZombie.Contains(userId);

        // ===== 设置构建 =====

        private static List<SettingBase> BuildSettings()
        {
            var header = new HeaderSetting(IdHeader, "SCP-049-2 感染", padding: true);

            var infect = new KeybindSetting(
                IdInfect,
                "感染",
                KeyCode.Alpha5,
                preventInteractionOnGUI: false,
                allowSpectatorTrigger: false,
                collectionId: 255,
                header: header,
                onChanged: (player, setting) =>
                {
                    if (player == null) return;
                    if (setting is not KeybindSetting kb) return;
                    if (!kb.IsPressed) return;
                    TryInfect(player);
                });

            // Header 仅通过选项的 header: 参数关联（避免分组标题渲染两次）
            return new List<SettingBase> { infect };
        }

        /// <summary>049-2 出生时注册按键</summary>
        public static void RegisterKeys(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected) return;
                if (PlayerSettings.ContainsKey(player.UserId)) return;
                var settings = BuildSettings();
                SettingBase.Register(player, settings);
                PlayerSettings[player.UserId] = settings;
            }
            catch (Exception ex) { Log.Warn($"[SCPplaus] 注册感染按键失败: {ex.Message}"); }
        }

        /// <summary>玩家死亡/离开时注销按键</summary>
        public static void UnregisterKeys(Player player)
        {
            try
            {
                if (player == null) return;
                if (PlayerSettings.TryGetValue(player.UserId, out var settings))
                {
                    SettingBase.Unregister(player, settings);
                    PlayerSettings.Remove(player.UserId);
                }
            }
            catch { }
        }

        public static void RemoveFromAll()
        {
            try { SettingBase.Unregister(p => true, PlayerSettings.Values.SelectMany(s => s).ToList()); } catch { }
            PlayerSettings.Clear();
        }

        // ===== 感染逻辑 =====

        /// <summary>小僵尸使用感染技能（每僵尸限一次）：选一名观战玩家变为 049-2（200 血）</summary>
        public static void TryInfect(Player zombie)
        {
            try
            {
                if (zombie == null || !zombie.IsConnected || !zombie.IsAlive) return;
                if (zombie.Role.Type != RoleTypeId.Scp0492) return;

                // 每个小僵尸只能用一次
                if (UsedInfection.Contains(zombie.UserId))
                {
                    zombie.ShowHint("<color=#FF4444>[感染] 你已经用过感染技能了</color>", 2f);
                    return;
                }

                // 找一名观战玩家（距离最近的）
                Player target = null;
                float best = float.MaxValue;
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected || p.IsAlive) continue;
                    if (p.Role.Type != RoleTypeId.Spectator) continue;
                    float d = Vector3.Distance(zombie.Position, p.Position);
                    if (d < best) { best = d; target = p; }
                }

                if (target == null)
                {
                    zombie.ShowHint("<color=#FF4444>[感染] 当前没有可感染的观战玩家</color>", 2f);
                    return;
                }

                // 消耗次数
                UsedInfection.Add(zombie.UserId);

                // 变僵尸
                InfectedByZombie.Add(target.UserId);
                target.Role.Set(RoleTypeId.Scp0492, SpawnReason.ForceClass, RoleSpawnFlags.None);

                Timing.CallDelayed(0.6f, () =>
                {
                    try
                    {
                        if (target == null || !target.IsConnected) return;
                        // 被小僵尸感染的固定 200 血
                        target.MaxHealth = 200f;
                        target.Health = 200f;

                        // 强制手持 COM-15（与 049-2 一致）
                        try
                        {
                            var item = target.AddItem(ItemType.GunCOM15);
                            if (item != null) target.CurrentItem = item;
                        }
                        catch { }

                        // 注册感染按键（被感染者也有一次感染机会）
                        RegisterKeys(target);
                        InfectedByZombie.Add(target.UserId);

                        target.Broadcast(5, "<color=#FF69B4>[感染]</color> 你已被小僵尸感染为 SCP-049-2（200 血）");
                        zombie.ShowHint($"<color=#FF69B4>[感染]</color> 成功感染 {target.Nickname}", 3f);
                        Log.Info($"[SCPplaus] 小僵尸 {zombie.Nickname} 感染了 {target.Nickname}");
                    }
                    catch (Exception ex) { Log.Error($"[SCPplaus] 感染后初始化失败: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Log.Error($"[SCPplaus] 感染技能失败: {ex.Message}"); }
        }

        public static void Reset()
        {
            UsedInfection.Clear();
            InfectedByZombie.Clear();
        }
    }

    /// <summary>
    /// SCP-1509（旧报纸）手持加速：手持时获得 20% 移速加成。
    /// 由 SCPplaus 的周期协程每秒刷新（短时长防残留）。
    /// </summary>
    public static class Scp1509SpeedBoost
    {
        public const byte BoostIntensity = 20;      // 20% 加速
        public const float RefreshDuration = 1.6f;  // 每秒刷新、持续 1.6s 防止闪烁

        public static void Tick()
        {
            try
            {
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected || !p.IsAlive) continue;
                    var cur = p.CurrentItem;
                    if (cur != null && cur.Type == ItemType.SCP1509)
                        p.EnableEffect(EffectType.MovementBoost, BoostIntensity, RefreshDuration);
                }
            }
            catch { }
        }
    }
}
