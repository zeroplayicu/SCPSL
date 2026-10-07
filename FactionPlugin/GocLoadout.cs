using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;

namespace FactionPlugin
{
    /// <summary>
    /// GOC 各兵种的装备与基础属性配置。
    /// 说明：枪械"弹匣容量"由客户端模型与服务端同步，无法动态修改；
    /// 战斗专家/指挥官使用 FR-MG-0（ItemType.GunFRMG0），伤害通过伤害事件单独修正。
    /// </summary>
    public static class GocLoadout
    {
        /// <summary>SCP-207 常驻时长（秒），视为"携带即生效"</summary>
        private const float InfEffectDuration = 3599f;

        public static void Apply(Player player, GocRoleType roleType)
        {
            try
            {
                try { player.ClearInventory(); } catch { }

                switch (roleType)
                {
                    case GocRoleType.Soldier: ApplySoldier(player); break;
                    case GocRoleType.Vanguard: ApplyVanguard(player); break;
                    case GocRoleType.Heavy: ApplyHeavy(player); break;
                    case GocRoleType.Breacher: ApplyBreacher(player); break;
                    case GocRoleType.SpecialOps: ApplySpecialOps(player); break;
                    case GocRoleType.Medic: ApplyMedic(player); break;
                    case GocRoleType.Commander: ApplyCommander(player); break;
                    case GocRoleType.Thaumaturge: ApplyThaumaturge(player); break;
                }

                // ===== 统一发放：重型护甲（2026-10-06 用户要求：每个 GOC 角色都有重甲）=====
                try { player.AddItem(ItemType.ArmorHeavy); } catch (Exception ex) { Log.Debug($"[GOC] 重甲发放失败: {ex.Message}"); }

                // ===== GOC 统一移速加成（替代 SCP-207 效果）=====
                // 先锋例外：它基础移速被压到 75%，不给这个 +20% 加成
                if (roleType != GocRoleType.Vanguard)
                    EnableInfEffect(player, EffectType.MovementBoost, 20);
            }
            catch (Exception ex)
            {
                Log.Error($"[GOC] 发放装备失败: {ex.Message}");
            }
        }

        /// <summary>士兵（2 名）：E11（伤害 45）+ 1x SCP-207</summary>
        /// <summary>先锋（1 名）：与士兵同款 E11 + 移速降至 75%（Slowness 25）；技能1 超负荷 → 移速 250%、120 秒后自爆</summary>
        private static void ApplyVanguard(Player player)
        {
            player.AddItem(ItemType.GunE11SR);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.KeycardMTFOperative);

            // 基础移速 75%（减速 25%），超负荷时移除
            EnableInfEffect(player, EffectType.Slowness, 25);
        }

        private static void ApplySoldier(Player player)
        {
            player.AddItem(ItemType.GunE11SR);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.KeycardMTFOperative);

            // 207 改为移速加成（在 Apply 尾部统一处理）
        }

        /// <summary>重装（1 名）：300 Hume Shield（HS，非 AHP）、无 SCP-207、重甲</summary>
        private static void ApplyHeavy(Player player)
        {
            player.AddItem(ItemType.GunE11SR);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.KeycardMTFOperative);

            // 可恢复护盾 300 Hume Shield
            try { player.HumeShield = 300f; } catch (Exception ex) { Log.Debug($"[GOC] 重装 HS 设置失败: {ex.Message}"); }
        }

        /// <summary>战斗专家（1 名）：FR-MG-0（伤害 65 / 对 SCP 150 无视护盾）、2x SCP-207、35% 减伤</summary>
        /// <summary>特战（1 名）：特殊囚鸟（500 伤害/不可损坏/无法蓄力）+ 特殊手枪（50 伤害/12 发弹匣）</summary>
        private static void ApplySpecialOps(Player player)
        {
            // 特殊囚鸟
            player.AddItem(ItemType.Jailbird);
            // 特殊手枪（Com-15，弹匣改为 12 发）
            var pistol = player.AddItem(ItemType.GunCOM15);
            try
            {
                var fa = pistol?.As<Exiled.API.Features.Items.Firearm>();
                if (fa != null)
                {
                    fa.MaxMagazineAmmo = 12;
                    fa.MagazineAmmo = 12;
                }
            }
            catch (Exception ex) { Log.Debug($"[GOC] 特战手枪弹匣设置失败: {ex.Message}"); }

            player.AddItem(ItemType.Ammo9x19);
            player.AddItem(ItemType.Ammo9x19);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.KeycardMTFOperative);
        }

        private static void ApplyBreacher(Player player)
        {
            player.AddItem(ItemType.GunFRMG0);
            player.AddItem(ItemType.Ammo762x39);
            player.AddItem(ItemType.Ammo762x39);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.KeycardMTFOperative);

            // 207 改为移速加成（在 Apply 尾部统一处理）
        }

        /// <summary>医疗兵（1 名）：1x SCP-207 + 医疗物资</summary>
        private static void ApplyMedic(Player player)
        {
            player.AddItem(ItemType.GunE11SR);
            player.AddItem(ItemType.Ammo556x45);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.Painkillers);
            player.AddItem(ItemType.KeycardMTFOperative);

            // 207 改为移速加成（在 Apply 尾部统一处理）
        }

        /// <summary>指挥官（1 名）：FR-MG-0（伤害 75）、3x SCP-207、SCP-500、450 Hume Shield</summary>
        private static void ApplyCommander(Player player)
        {
            player.AddItem(ItemType.GunFRMG0);
            player.AddItem(ItemType.Ammo762x39);
            player.AddItem(ItemType.Ammo762x39);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.SCP500);      // 洗手液（SCP-500）
            player.AddItem(ItemType.KeycardO5);

            // 207 改为移速加成（在 Apply 尾部统一处理）

            try { player.HumeShield = 450f; } catch (Exception ex) { Log.Debug($"[GOC] 指挥官 HS 设置失败: {ex.Message}"); }
        }

        /// <summary>奇术师（1 名）：A7（常驻开火：子弹无限、消耗 HS 1发/1HS、无瞄准全向命中、伤害 30）+ 150 HS</summary>
        private static void ApplyThaumaturge(Player player)
        {
            player.AddItem(ItemType.GunA7);
            // 不发放原生弹药：唯一"弹药"是 HS 护盾（1HS = 1 发，护盾 0 则无法射出）
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.KeycardMTFOperative);

            // 207 改为移速加成（在 Apply 尾部统一处理）

            try { player.HumeShield = 150f; } catch (Exception ex) { Log.Debug($"[GOC] 奇术师 HS 设置失败: {ex.Message}"); }
        }

        /// <summary>开启常驻效果（时长视为永久）</summary>
        private static void EnableInfEffect(Player player, EffectType type, byte intensity)
        {
            try { player.EnableEffect(type, intensity, InfEffectDuration); }
            catch (Exception ex) { Log.Debug($"[GOC] 效果 {type} 启用失败: {ex.Message}"); }
        }
    }
}
