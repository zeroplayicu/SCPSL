using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// GOC 枪械伤害补足（2026-10-02）。
    ///
    /// 问题：GOC 载体是 Tutorial 角色 —— 游戏不触发 Shooting 事件，且原生武器伤害对多数目标
    /// （尤其混沌分裂者）被游戏层面吞掉，导致 GOC 开枪打不出伤害。
    ///
    /// 方案：0.1 秒轮询监视弹匣变化判定"开火"，随后做**视线射线检测**命中目标，
    /// 直接用 `Player.Hurt(..., DamageType.Custom)` 主动施加伤害（绕过原生阵营判定）。
    /// 配套：`GocDamageHandler` 会把 GOC 的**原生** Firearm 伤害归零，避免与这里重复计算。
    /// </summary>
    public static class GocGunFireWatcher
    {
        /// <summary>每人每把枪上次的弹匣余弹</summary>
        private static readonly Dictionary<string, int> LastAmmo = new Dictionary<string, int>();
        private static readonly Dictionary<string, ItemType> LastWeapon = new Dictionary<string, ItemType>();

        private const float MaxRange = 100f;

        public static void Tick()
        {
            try
            {
                TickNu7();

                if (GocManager.Members.Count == 0) return;

                foreach (var kv in GocManager.Members.ToList())
                {
                    var p = Player.Get(kv.Key);
                    if (p == null || !p.IsConnected || !p.IsAlive)
                    {
                        Cleanup(kv.Key);
                        continue;
                    }

                    // 奇术师单独走 A7 全向命中逻辑，这里跳过
                    if (kv.Value == GocRoleType.Thaumaturge) { Cleanup(kv.Key); continue; }

                    var item = p.CurrentItem;
                    Exiled.API.Features.Items.Firearm fa = null;
                    try { if (item != null && item.IsWeapon) fa = item.As<Exiled.API.Features.Items.Firearm>(); } catch { }

                    // SCP-1509（旧报纸，近战）伤害由原生流程 + GocDamageHandler 处理，不在此补
                    if (item == null || fa == null || item.Type == ItemType.SCP1509)
                    {
                        Cleanup(kv.Key);
                        continue;
                    }

                    // 换枪则重置记录
                    if (LastWeapon.TryGetValue(kv.Key, out ItemType lastType) && lastType != item.Type)
                    {
                        LastWeapon[kv.Key] = item.Type;
                        LastAmmo[kv.Key] = fa.MagazineAmmo;
                        continue;
                    }
                    LastWeapon[kv.Key] = item.Type;

                    int cur = fa.MagazineAmmo;
                    if (!LastAmmo.TryGetValue(kv.Key, out int last))
                    {
                        LastAmmo[kv.Key] = cur;
                        continue;
                    }

                    if (cur < last)
                    {
                        // 弹匣减少 = 玩家开火了
                        int shots = last - cur;
                        for (int i = 0; i < shots; i++)
                            ProcessOneShot(p, kv.Value, item.Type);
                    }
                    LastAmmo[kv.Key] = fa.MagazineAmmo;
                }
            }
            catch (Exception ex) { Log.Debug($"[GOC] 枪械开火检测失败: {ex.Message}"); }
        }

        /// <summary>NU7-A 成员的枪械伤害（2026-10-06）：同样用弹匣轮询 + 射线命中</summary>
        private static void TickNu7()
        {
            try
            {
                if (Nu7Manager.Members.Count == 0) return;

                foreach (var kv in Nu7Manager.Members.ToList())
                {
                    var p = Player.Get(kv.Key);
                    if (p == null || !p.IsConnected || !p.IsAlive) { Cleanup(kv.Key); continue; }

                    var item = p.CurrentItem;
                    Exiled.API.Features.Items.Firearm fa = null;
                    try { if (item != null && item.IsWeapon) fa = item.As<Exiled.API.Features.Items.Firearm>(); } catch { }
                    if (item == null || fa == null || item.Type == ItemType.SCP1509) { Cleanup(kv.Key); continue; }

                    if (LastWeapon.TryGetValue(kv.Key, out ItemType lt) && lt != item.Type)
                    {
                        LastWeapon[kv.Key] = item.Type; LastAmmo[kv.Key] = fa.MagazineAmmo; continue;
                    }
                    LastWeapon[kv.Key] = item.Type;

                    int cur = fa.MagazineAmmo;
                    if (!LastAmmo.TryGetValue(kv.Key, out int last)) { LastAmmo[kv.Key] = cur; continue; }

                    if (cur < last)
                    {
                        int shots = last - cur;
                        for (int i = 0; i < shots; i++) ProcessNu7Shot(p, kv.Value, item.Type);
                    }
                    LastAmmo[kv.Key] = fa.MagazineAmmo;
                }
            }
            catch (Exception ex) { Log.Debug($"[NU7-A] 开火检测失败: {ex.Message}"); }
        }

        private static void ProcessNu7Shot(Player attacker, Nu7RoleType role, ItemType weapon)
        {
            try
            {
                Player target = RaycastTarget(attacker);
                if (target == null || target == attacker) return;
                if (Nu7Manager.IsNu7(target)) return;          // 内部无友伤
                if (GocManager.IsGoc(target)) return;          // 与 GOC 也不互伤

                float dmg = Nu7Manager.GetWeaponDamage(role, weapon, target.IsScp);
                if (dmg <= 0f) return;

                target.Hurt(attacker, dmg, DamageType.Custom);
            }
            catch (Exception ex) { Log.Debug($"[NU7-A] 命中处理失败: {ex.Message}"); }
        }

        private static void Cleanup(string userId)
        {
            LastAmmo.Remove(userId);
            LastWeapon.Remove(userId);
        }

        /// <summary>单发：视线射线命中判定 → 按兵种规则施加伤害</summary>
        private static void ProcessOneShot(Player attacker, GocRoleType role, ItemType weapon)
        {
            try
            {
                Player target = RaycastTarget(attacker);
                if (target == null) return;
                if (target == attacker) return;
                if (GocManager.IsGoc(target)) return;          // 内部无友伤

                float damage = GetDamage(role, weapon, target);
                if (damage <= 0f) return;

                // 用 Custom 类型施加：既绕过原生阵营判定，也能与"原生 Firearm 伤害归零"区分开
                target.Hurt(attacker, damage, DamageType.Custom);

                // 「左轮」技能：命中敌人后武器消失（一次性）
                if (weapon == ItemType.GunRevolver)
                {
                    try
                    {
                        var rev = attacker.Items.FirstOrDefault(i => i.Type == ItemType.GunRevolver);
                        if (rev != null) attacker.RemoveItem(rev);
                        attacker.ShowHint("<color=#FFD700>[左轮]</color> 命中！左轮已消耗消失", 3f);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log.Debug($"[GOC] 射击命中处理失败: {ex.Message}"); }
        }

        /// <summary>视线射线：取玩家摄像机射线命中的第一个玩家</summary>
        private static Player RaycastTarget(Player attacker)
        {
            try
            {
                var cam = attacker.CameraTransform;
                if (cam == null) return null;

                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, MaxRange,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    var hub = hit.collider.GetComponentInParent<ReferenceHub>();
                    if (hub != null)
                    {
                        var p = Player.Get(hub);
                        if (p != null && p.IsConnected && p.IsAlive) return p;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>兵种武器伤害表（与 GocDamageHandler 保持一致）</summary>
        private static float GetDamage(GocRoleType role, ItemType weapon, Player target)
        {
            if (weapon == ItemType.SCP1509) return 180f;                     // 战斗专家爆发
            if (weapon == ItemType.GunRevolver) return 1500f;                // 「左轮」技能：一次性 1500

            switch (role)
            {
                case GocRoleType.Soldier:
                case GocRoleType.Vanguard:
                    if (weapon == ItemType.GunE11SR) return 45f;
                    break;
                case GocRoleType.Breacher:
                    if (weapon == ItemType.GunFRMG0) return target.IsScp ? 150f : 65f;
                    break;
                case GocRoleType.Commander:
                    if (weapon == ItemType.GunFRMG0) return 75f;
                    break;
                case GocRoleType.SpecialOps:
                    if (weapon == ItemType.GunCOM15) return 50f;      // 特战手枪
                    break;
            }
            return 0f;   // 无特殊规则：不额外施加（交给原生流程）
        }
    }
}
