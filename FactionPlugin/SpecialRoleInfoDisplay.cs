using System;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace FactionPlugin
{
    /// <summary>
    /// 特殊角色信息层：显示在屏幕底部（等级状态栏下方，Y=955）。
    /// 内容为当前身份（GOC 兵种 / SCP-999 / SCP-181）+ 技能介绍。
    /// 需每秒刷新（hint 有有效期），由 FactionPlugin.CooldownRoutine 调用。
    /// </summary>
    public static class SpecialRoleInfoDisplay
    {
        private const string LayerId = "special_role_info";
        private const int InfoY = 955;

        public static void Refresh(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected || !player.IsAlive) return;
                // 特殊角色信息层只在 7779 实例启用
                if (!FactionPlugin.SpecialRolesEnabled) return;

                // 只有特殊角色（GOC / SCP-999 / SCP-181）才需要该显示层
                bool isGoc = GocManager.Members.ContainsKey(player.UserId);
                bool is999 = Scp999Manager.IsScp999(player);
                bool is181 = Scp181Manager.Is181(player);
                if (!isGoc && !is999 && !is181) return;

                string text = BuildText(player);
                if (string.IsNullOrEmpty(text)) return;

                var disp = PlayerDisplay.Get(player);
                disp.RemoveHint(LayerId);   // HSM 同 Id 不替换，必须先移除旧实例再显示
                disp.ShowHint(new HsmHint
                {
                    Id = LayerId,
                    Text = text,
                    FontSize = 13,
                    YCoordinate = InfoY,
                    Alignment = HintAlignment.Center
                }, 3f);
            }
            catch (Exception ex)
            {
                Log.Debug($"[特殊角色] 信息层刷新失败: {ex.Message}");
            }
        }

        public static void Clear(Player player)
        {
            try
            {
                if (player == null) return;
                PlayerDisplay.Get(player).RemoveHint(LayerId);
            }
            catch { }
        }

        /// <summary>构建身份 + 技能介绍文本（GOC 优先，其次 SCP-999 / SCP-181）</summary>
        private static string BuildText(Player player)
        {
            // NU7-A 阵营介绍（2026-10-06）
            if (Nu7Manager.IsNu7(player)) return Nu7Manager.GetIntro(player);
            // ===== GOC =====
            if (GocManager.Members.TryGetValue(player.UserId, out GocRoleType role))
            {
                switch (role)
                {
                    case GocRoleType.Soldier:
                        return "<color=#00AEEF>[GOC 士兵]</color> <color=#AAAAAA>E11（伤害 45） · 无主动技能 · 免坠落伤害</color>";
                    case GocRoleType.Vanguard:
                        return "<color=#00AEEF>[GOC 先锋]</color> <color=#AAAAAA>E11 · 移速 75%</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>超负荷</color> <color=#AAAAAA>移速飙至 250%，120 秒后自爆死亡（一次性）</color>";
                    case GocRoleType.Heavy:
                        return "<color=#00AEEF>[GOC 重装]</color> <color=#AAAAAA>300 护盾（静止 5 秒后回盾） · 无主动技能 · 免坠落伤害</color>";
                    case GocRoleType.Breacher:
                        return "<color=#00AEEF>[GOC 战斗专家]</color> <color=#AAAAAA>被动减伤 35%</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>抗性</color> <color=#AAAAAA>减伤 75% + 移速 +45% 共25秒 CD120秒</color>";
                    case GocRoleType.Medic:
                        return "<color=#00AEEF>[GOC 医疗兵]</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>范围治疗</color> <color=#AAAAAA>队友8HP/s 共15秒 CD30秒</color>  <color=#FFD700>[技能2]</color> <color=#FFFFFF>肾上腺素</color> <color=#AAAAAA>全体50AHP+加速 共10秒 CD45秒</color>";
                    case GocRoleType.Commander:
                        return "<color=#00AEEF>[GOC 指挥官]</color> <color=#AAAAAA>450 护盾（静止回盾）</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>隐身</color> <color=#AAAAAA>15秒 CD60秒</color>  <color=#FFD700>[技能2]</color> <color=#FFFFFF>激励</color> <color=#AAAAAA>全体加速+100AHP 共15秒 CD120秒</color>";
                    case GocRoleType.SpecialOps:
                        return "<color=#00AEEF>[GOC 特战]</color> <color=#AAAAAA>囚鸟(500伤/打不爆/无法蓄力) + 手枪(50伤/12发)</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>急急急</color> <color=#AAAAAA>移速 +150% 共15秒 CD45秒</color>  <color=#FFD700>[技能2]</color> <color=#FFFFFF>左轮</color> <color=#AAAAAA>1发/1500伤，命中敌人后消失，CD120秒</color>";
                    case GocRoleType.Thaumaturge:
                        return "<color=#00AEEF>[GOC 奇术师]</color> <color=#AAAAAA>150 护盾（静止回盾）</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>困住</color> <color=#AAAAAA>10m 内敌人定身15秒 CD100秒</color>  <color=#FFD700>[技能2]</color> <color=#FFFFFF>震击</color> <color=#AAAAAA>5m 内每秒-15HP 共20秒 CD100秒</color>  <color=#FFD700>A7 常驻开火</color> <color=#AAAAAA>鼠标左键直接射击 · 子弹无限消耗护盾(1HS=1发) · 全向命中伤害30 · 无盾无法射击</color>";
                }
            }

            // ===== SCP-999 =====
            if (Scp999Manager.IsScp999(player))
            {
                return "<color=#FF69B4>[SCP-999]</color> <color=#FFFFFF>友好单位</color> <color=#FFD700>[技能1]</color> <color=#AAAAAA>附近人类肾上腺素</color> <color=#FFD700>[技能2]</color> <color=#AAAAAA>范围回复</color> <color=#AAAAAA>· 靠近友军持续治疗 · 攻击他人转为治疗 · 持灯每秒回血</color>";
            }

            // ===== SCP-181 =====
            if (Scp181Manager.Is181(player))
            {
                return "<color=#FFD700>[SCP-181 幸运儿]</color> <color=#AAAAAA>35% 免疫伤害 · 15% 无卡开门/收容柜/核弹面板 · 10% 拾取物品翻倍或升级</color>";
            }

            return null;
        }
    }
}