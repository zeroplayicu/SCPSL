using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    public class FactionEventHandler
    {
        private readonly FactionPlugin _plugin;
        private readonly Scp999HudDisplay _hudDisplay;

        public FactionEventHandler(FactionPlugin plugin, Scp999HudDisplay hudDisplay)
        {
            _plugin = plugin;
            _hudDisplay = hudDisplay;
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            if (ev.Player == null || !ev.Player.IsConnected) return;

            // 清掉所有原生/插件历史广播（含观战裂变、出生提示等）
            ev.Player.ClearBroadcasts();

            // GOC 成员出现但角色既不是 Tutorial（阶段一管理塔）也不是 ChaosRifleman（阶段二混沌点）
            // → 说明被其他变身覆盖（999/181/ClassD 等），清除残留标志
            if (GocManager.IsGoc(ev.Player) &&
                ev.Player.Role.Type != RoleTypeId.Tutorial &&
                ev.Player.Role.Type != RoleTypeId.ChaosRifleman)
                GocManager.ClearBadge(ev.Player);

            // SCP-999玩家保持0.5倍体型；其他玩家恢复默认体型
            if (Scp999Manager.IsScp999(ev.Player))
            {
                ev.Player.Scale = new Vector3(0.5f, 0.5f, 0.5f);
                // 立即刷新一次 HUD，避免等满 1 秒的协程周期
                _hudDisplay?.Refresh(ev.Player);
            }
            else
            {
                ev.Player.Scale = Vector3.one;
                // 变回观察者时清掉 HUD
                _hudDisplay?.Clear(ev.Player);
            }
        }

        public void OnDied(DiedEventArgs ev)
        {
            if (ev.Player == null) return;

            // SCP-999死亡后恢复体型并移除SCP-999状态
            Scp999Manager.ResetPlayer(ev.Player);
            // 清理屏幕底部 HUD（变回观察者后不再显示技能信息）
            _hudDisplay?.Clear(ev.Player);
            // GOC 死亡：彻底清除标志（RankName/CustomInfo/成员记录/技能按键）
            GocManager.ClearBadge(ev.Player);
            // 特殊角色介绍层一并清除
            SpecialRoleInfoDisplay.Clear(ev.Player);
            // 阵营人数 HUD 清除
            FactionCountDisplay.Clear(ev.Player);
        }

        public void OnLeft(LeftEventArgs ev)
        {
            if (ev.Player == null) return;

            // 玩家离开时清理SCP-999状态与体型
            Scp999Manager.ResetPlayer(ev.Player);
            _hudDisplay?.Clear(ev.Player);
            // 同步清理 GOC 成员记录
            GocManager.OnPlayerLeft(ev.Player);
            // SCP-181
            Scp181Manager.OnPlayerLeft(ev.Player);
            // 阵营人数 HUD 清除
            FactionCountDisplay.Clear(ev.Player);
        }

        /// <summary>999 攻击后待回血记录：victim userId → 治疗量</summary>
        private static readonly Dictionary<string, float> PendingHeal = new Dictionary<string, float>();

        /// <summary>判断物品是否为 SCP-999 可拾取的武器</summary>
        public bool IsScp999Weapon(ItemType type) => Scp999Weapons.Contains(type);

        /// <summary>SCP-999 可拾取的武器类型（武器攻击伤害转为目标回血）</summary>
        private static readonly HashSet<ItemType> Scp999Weapons = new HashSet<ItemType>
        {
            ItemType.GunE11SR, ItemType.GunFRMG0, ItemType.GunAK, ItemType.GunA7,
            ItemType.GunCOM15, ItemType.GunCOM18, ItemType.GunCrossvec, ItemType.GunFSP9,
            ItemType.GunLogicer, ItemType.GunRevolver, ItemType.GunShotgun, ItemType.GunSCP127
        };

        /// <summary>受伤后：999 的攻击转为回血 + 给攻击者显示目标血量</summary>
        public void OnHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Player == null || ev.Attacker == null) return;

                // ===== 通用：攻击者可见被攻击者名字/剩余血量/造成的伤害 =====
                // （999 除外——它有专属的治疗显示；NPC 攻击也排除）
                if (ev.Attacker != ev.Player && !ev.Attacker.IsNPC && !Scp999Manager.IsScp999(ev.Attacker))
                {
                    ev.Attacker.ShowHint(
                        $"<color=#FF6666>[攻击]</color> {ev.Player.Nickname} 血量 <color=#44FF88>{ev.Player.Health:0}</color><color=#AAAAAA>/{ev.Player.MaxHealth:0}</color>（伤害 <color=#FF6666>{ev.Amount:0}</color>）",
                        2f);
                }

                // ===== SCP-999 武器攻击：转为治疗 =====
                if (!PendingHeal.TryGetValue(ev.Player.UserId, out float heal)) return;
                PendingHeal.Remove(ev.Player.UserId);
                if (!Scp999Manager.IsScp999(ev.Attacker)) return;

                // 原生已扣 1 点反馈伤害 → 补回它并加上治疗量（净回血 = 原武器伤害）
                float newHp = Math.Min(ev.Player.MaxHealth, ev.Player.Health + heal + 1f);
                ev.Player.Health = newHp;

                // 被攻击者的受击提示
                ev.Player.ShowHint($"<color=#FF69B4>[SCP-999 治疗]</color> <color=#44FF88>+{heal:0} HP</color>", 2f);

                // 攻击者（999）查看目标血量
                ev.Attacker.ShowHint(
                    $"<color=#FF69B4>[SCP-999 治疗]</color> {ev.Player.Nickname} 血量 <color=#44FF88>{ev.Player.Health:0}</color><color=#AAAAAA>/{ev.Player.MaxHealth:0}</color>（治疗 +{heal:0}）",
                    3f);
            }
            catch (Exception ex) { Log.Debug($"[999] 转治疗失败: {ex.Message}"); }
        }

        /// <summary>奇术师特制 A7 不再依赖 Shooting 事件（Tutorial 角色不触发），改用 SSS 技能键</summary>
        /// <summary>特战囚鸟：无法蓄力攻击（2026-10-02）</summary>
        public void OnChargingJailbird(ChargingJailbirdEventArgs ev)
        {
            if (ev.Player == null) return;
            if (!GocManager.Members.TryGetValue(ev.Player.UserId, out GocRoleType r) || r != GocRoleType.SpecialOps) return;
            ev.IsAllowed = false;
            ev.Player.ShowHint("<color=#FF8844>[囚鸟]</color> 无法蓄力攻击", 1.5f);
        }

        /// <summary>特战囚鸟：打不爆（阻止磨损/损坏状态变化）</summary>
        public void OnJailbirdChangingWear(JailbirdChangingWearStateEventArgs ev)
        {
            if (ev.Player == null) return;
            if (!GocManager.Members.TryGetValue(ev.Player.UserId, out GocRoleType r) || r != GocRoleType.SpecialOps) return;
            ev.IsAllowed = false;
        }

        public void OnShooting(ShootingEventArgs ev)
        {
            // 留空：原方案对 Tutorial 角色失效
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null) return;

            // ===== SCP-999 武器攻击 = 治疗（伤害转为目标回血）=====
            if (ev.Attacker != null && ev.Attacker != ev.Player && Scp999Manager.IsScp999(ev.Attacker))
            {
                // 记录治疗量（= 原伤害），保留 1 点伤害触发原生受击反馈（红屏/音效）
                PendingHeal[ev.Player.UserId] = ev.Amount;
                ev.Amount = 1f;
                // 不 return，让这 1 点伤害走正常流程
            }

            // GOC 阵营伤害规则（武器伤害修正 / 战斗专家减伤 / 内部无友伤）
            GocDamageHandler.Apply(ev);

            // SCP-999 受伤害减半（Hurting 阶段直接修改伤害值，对环境伤害与致死一击均生效）
            if (Scp999Manager.IsScp999(ev.Player))
                ev.Amount *= 0.5f;

            if (ev.Attacker == null) return;

            // SCP-999 攻击造成固定5点伤害
            if (Scp999Manager.IsScp999(ev.Attacker))
                ev.Amount = _plugin.Config.Scp999Damage;
        }

        public void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            // 点歌台：附近按 E 切歌（2026-10-06）
            if (MusicStation.TryInteract(ev.Player)) return;

            if (ev.Player == null || ev.Door == null) return;

            // 只对SCP-999生效：修复没有钥匙卡也能开门的问题
            if (!Scp999Manager.IsScp999(ev.Player)) return;

            // 没有钥匙卡 → 禁止开门（SCP-999无卡不能开需要卡的门）
            if (!HasKeycard(ev.Player))
            {
                ev.IsAllowed = false;
                ev.Player.ShowHint("<color=#FF4444>[SCP-999] 需要钥匙卡才能开门</color>", 1.5f);
            }
        }

        /// <summary>判断玩家背包是否持有门禁卡（ItemType 名称含 "Keycard"）</summary>
        private static bool HasKeycard(Player player)
        {
            try
            {
                if (player == null || player.Items == null) return false;
                foreach (var item in player.Items)
                {
                    if (item == null) continue;
                    if (item.Type.ToString().IndexOf("Keycard", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }

        public void OnRoundStarted()
        {
            Scp999Manager.Reset();
            Scp999SkillManager.Reset();
            // GOC 阵营：回合开始重置成员与刷新标记
            GocManager.OnRoundStarted();
            Log.Info("[FactionPlugin] 新回合已重置");

            // 延迟到回合开始后再尝试开局自动刷新SCP-999
            Timing.CallDelayed(3f, () =>
            {
                Scp999Manager.TryAutoSpawn();
            });
        }
    }
}
