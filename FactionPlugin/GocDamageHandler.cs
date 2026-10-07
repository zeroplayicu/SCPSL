using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features.DamageHandlers;

namespace FactionPlugin
{
    /// <summary>
    /// GOC 阵营伤害规则：
    ///   - 士兵 E11         → 45 伤害
    ///   - 战斗专家 FR-MG-0 → 对 SCP 150（无视护盾）/ 对人类 65
    ///   - 指挥官 FR-MG-0   → 75 伤害
    ///   - SCP-1509（爆发） → 180 伤害
    ///   - 战斗专家受到伤害 → 减免 35%
    ///   - GOC 内部           → 无友伤
    /// </summary>
    public static class GocDamageHandler
    {
        public static void Apply(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;

                // 0) GOC 免坠落伤害
                if (GocManager.IsGoc(ev.Player) && ev.DamageHandler?.Type == Exiled.API.Enums.DamageType.Falldown)
                {
                    ev.IsAllowed = false;
                    return;
                }

                // 1) GOC 内部无友伤
                if (ev.Attacker == null)
                {
                    // 没有攻击者（环境伤害）也应用战斗专家减伤（技能「抗性」期间 75%）
                    if (GocManager.Members.TryGetValue(ev.Player.UserId, out GocRoleType vr0) && vr0 == GocRoleType.Breacher)
                        ev.Amount *= GocSkillManager.IsBreacherGuarding(ev.Player) ? 0.25f : 0.65f;
                    return;
                }

                // 1) GOC 内部无友伤
                if (GocManager.IsGoc(ev.Attacker) && GocManager.IsGoc(ev.Player))
                {
                    ev.IsAllowed = false;
                    return;
                }

                // 2) GOC 攻击者的武器伤害修正
                if (GocManager.Members.TryGetValue(ev.Attacker.UserId, out GocRoleType role))
                {
                    // 原生「枪械」伤害一律取消：GOC 载体是 Tutorial，原生射击链路不可靠
                    // （打混沌等目标常被游戏吞掉），实际伤害统一由 GocGunFireWatcher 射线命中后施加
                    // Custom 类型伤害，避免双倍计算。SCP-1509 等非 Firearm 伤害不受影响。
                    if (ev.DamageHandler?.Type == Exiled.API.Enums.DamageType.Firearm)
                    {
                        ev.IsAllowed = false;
                        return;
                    }

                    bool scpTarget = ev.Player.IsScp;
                    float? overrideDamage = null;
                    bool ignoreShield = false;

                    ItemType? weapon = GetWeaponType(ev.Attacker);

                    if (weapon == ItemType.SCP1509)
                    {
                        // 战斗专家"爆发"期间强制手持的旧报纸
                        overrideDamage = 180f;
                    }
                    else if ((role == GocRoleType.Soldier || role == GocRoleType.Vanguard) && weapon == ItemType.GunE11SR)
                    {
                        overrideDamage = 45f;
                    }
                    else if (role == GocRoleType.Breacher && weapon == ItemType.GunFRMG0)
                    {
                        overrideDamage = scpTarget ? 150f : 65f;
                        ignoreShield = scpTarget;     // 对 SCP 无视护盾
                    }
                    else if (role == GocRoleType.Commander && weapon == ItemType.GunFRMG0)
                    {
                        overrideDamage = 75f;
                    }
                    else if (role == GocRoleType.SpecialOps && weapon == ItemType.Jailbird)
                    {
                        overrideDamage = 500f;      // 特殊囚鸟：500 伤害
                    }
                    else if (role == GocRoleType.SpecialOps && weapon == ItemType.GunCOM15)
                    {
                        overrideDamage = 50f;       // 特战手枪：50 伤害
                    }
                    else if (weapon == ItemType.GunRevolver)
                    {
                        overrideDamage = 1500f;     // 「左轮」技能：1500 伤害（一次性）
                    }
                    else if (role == GocRoleType.Thaumaturge && weapon == ItemType.GunA7)
                    {
                        // 奇术师特制 A7（实际伤害由 OnShooting 手动命中施加，这里兜底修正）
                        overrideDamage = 30f;
                    }

                    if (overrideDamage.HasValue)
                        ev.Amount = overrideDamage.Value;

                    if (ignoreShield && overrideDamage.HasValue)
                    {
                        // 无视护盾：取消原始伤害，直接扣除本体 HP（绕过 AHP / Hume Shield）
                        float dmg = overrideDamage.Value;
                        ev.IsAllowed = false;
                        float newHp = Math.Max(0f, ev.Player.Health - dmg);
                        ev.Player.Health = newHp;
                        if (newHp <= 0f)
                        {
                            try { ev.Player.Kill("GOC穿盾攻击"); } catch { }
                        }
                    }
                }

                // 3) 战斗专家抗性：平时减免 35%，技能「抗性」期间减免 75%（2026-10-02）
                if (GocManager.Members.TryGetValue(ev.Player.UserId, out GocRoleType victimRole) &&
                    victimRole == GocRoleType.Breacher)
                {
                    ev.Amount *= GocSkillManager.IsBreacherGuarding(ev.Player) ? 0.25f : 0.65f;
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"[GOC] 伤害修正失败: {ex.Message}");
            }
        }

        /// <summary>取攻击者当前手持武器类型（非武器返回 null）。
        /// 用当前手持物品判断而非伤害处理器，避免依赖 EXILED 内部成员名。</summary>
        private static ItemType? GetWeaponType(Player attacker)
        {
            try
            {
                var current = attacker.CurrentItem;
                if (current != null && current.IsWeapon)
                    return current.Type;
            }
            catch { }
            return null;
        }
    }
}
