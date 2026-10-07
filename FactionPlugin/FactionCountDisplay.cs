using System;
using System.Linq;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using PlayerRoles;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace FactionPlugin
{
    /// <summary>
    /// 屏幕右下角显示"当前阵营存活人数"。
    /// 2026-10-06 扩展：D 级 / MTF / 设施安保 / 科研 额外可见「混沌分裂者」人数；
    /// 混沌分裂者可见「D 级 + MTF」人数；GOC / SCP 只显示自己（红色）。
    /// 颜色：机动特遣队=蓝 / 设施安保=灰 / 科研=黄 / D级=橙 / 混沌=绿 / GOC=红 / SCP=红。
    /// </summary>
    public static class FactionCountDisplay
    {
        private const string LayerId = "faction_count";
        private const int InfoY = 1000;

        public static void Refresh(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected || !player.IsAlive) return;
                if (!FactionPlugin.SpecialRolesEnabled) return;

                string text = BuildText(player);
                if (string.IsNullOrEmpty(text)) return;

                var disp = PlayerDisplay.Get(player);
                disp.RemoveHint(LayerId);
                disp.ShowHint(new HsmHint
                {
                    Id = LayerId,
                    Text = text,
                    FontSize = 16,
                    YCoordinate = InfoY,
                    Alignment = HintAlignment.Right
                }, 3f);
            }
            catch (Exception ex) { Log.Debug($"[阵营人数] 刷新失败: {ex.Message}"); }
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

        private static string BuildText(Player player)
        {
            // ===== GOC =====
            if (GocManager.IsGoc(player))
            {
                int n = GocManager.Members.Keys.Count(id =>
                {
                    var p = Player.Get(id);
                    return p != null && p.IsConnected && p.IsAlive;
                });
                return $"<color=#FF3B3B>GOC[{n}]</color>";
            }

            // ===== SCP =====
            if (player.IsScp)
            {
                int n = Player.List.Count(p => p != null && p.IsAlive && p.IsScp);
                return $"<color=#FF3B3B>SCP[{n}]</color>";
            }

            RoleTypeId t = player.Role.Type;

            // ===== 机动特遣队（可见混沌人数）=====
            if (IsNtf(t))
            {
                int n = Player.List.Count(p => p != null && p.IsAlive && IsNtf(p.Role.Type));
                int sci = Player.List.Count(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.Scientist);
                int guard = Player.List.Count(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.FacilityGuard);
                return $"<color=#4AA5FF>机动特遣队[{n}]</color>\n" +
                       $"<color=#FFD700>科研[{sci}]</color>\n" +
                       $"<color=#9E9E9E>设施安保[{guard}]</color>";
            }

            // ===== 设施安保（可见混沌人数）=====
            if (t == RoleTypeId.FacilityGuard)
            {
                int n = Player.List.Count(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.FacilityGuard);
                return $"<color=#9E9E9E>设施安保[{n}]</color>\n{ChaosLine()}";
            }

            // ===== 科研（可见混沌人数）=====
            if (t == RoleTypeId.Scientist)
            {
                int n = Player.List.Count(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.Scientist);
                return $"<color=#FFD700>科研[{n}]</color>\n{ChaosLine()}";
            }

            // ===== D 级人员（可见混沌人数）=====
            if (t == RoleTypeId.ClassD)
            {
                int n = Player.List.Count(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.ClassD);
                return $"<color=#FF8C42>D级人员[{n}]</color>\n{ChaosLine()}";
            }

            // ===== 混沌分裂者（可见 D 级 + MTF 人数）=====
            if (IsChaos(t))
            {
                int chaos = Player.List.Count(p => p != null && p.IsAlive && IsChaos(p.Role.Type));
                int dCount = Player.List.Count(p => p != null && p.IsAlive && p.Role.Type == RoleTypeId.ClassD);
                int mtf = Player.List.Count(p => p != null && p.IsAlive && IsNtf(p.Role.Type));
                return $"<color=#00D26A>混沌分裂者[{chaos}]</color>\n" +
                       $"<color=#FF8C42>D级人员[{dCount}]</color>\n" +
                       $"<color=#4AA5FF>机动特遣队[{mtf}]</color>";
            }

            return null;
        }

        /// <summary>混沌分裂者人数行</summary>
        private static string ChaosLine()
        {
            int chaos = Player.List.Count(p => p != null && p.IsAlive && IsChaos(p.Role.Type));
            return $"<color=#00D26A>混沌分裂者[{chaos}]</color>";
        }

        private static bool IsNtf(RoleTypeId t) => t.ToString().StartsWith("Ntf");

        private static bool IsChaos(RoleTypeId t) => t.ToString().StartsWith("Chaos");
    }
}
