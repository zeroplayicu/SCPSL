using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace FactionPlugin
{
    /// <summary>
    /// 在屏幕底部中央常驻显示 SCP-999 的技能信息 + 角色介绍（HUD 形式，无需打开设置面板）。
    /// 使用 HSM 持久 Hint，仅在内容变化时重发，避免闪烁。
    /// 注意：HSM 不处理 Text 里的内联 &lt;size&gt; 标签，字号用 FontSize 控制，Text 只保留 &lt;color&gt;。
    /// </summary>
    public class Scp999HudDisplay
    {
        private readonly FactionPlugin _plugin;
        private readonly Dictionary<string, string> _lastTexts = new Dictionary<string, string>();
        private readonly Dictionary<string, DateTime> _lastSentAt = new Dictionary<string, DateTime>();

        // HSM Layer Id：作为常驻 HUD 层区分
        private const string LayerId = "scp999_role_hud";

        // hint 有效期 10 秒；即使内容没变也按此间隔重发，避免 hint 过期后永久消失
        private const double ResendIntervalSeconds = 5.0;

        public Scp999HudDisplay(FactionPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>
        /// 刷新单个 SCP-999 玩家的 HUD 显示（每 1 秒由 CooldownRoutine 调用）
        /// </summary>
        public void Refresh(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected || !Scp999Manager.IsScp999(player))
                    return;

                string text = BuildText(player);
                TryShowHint(player, text);
            }
            catch (Exception ex)
            {
                Log.Debug($"[SCP-999 HUD] 刷新失败: {ex.Message}");
            }
        }

        /// <summary>清空单个玩家的 HUD 显示（变回人类或死亡时调用）</summary>
        public void Clear(Player player)
        {
            try
            {
                if (player == null) return;

                string key = $"{player.UserId}|{LayerId}";
                _lastTexts.Remove(key);
                _lastSentAt.Remove(key);

                // 按 Layer Id 从 HSM 显示队列移除常驻 hint（比再压一个空白 hint 可靠）
                PlayerDisplay.Get(player).RemoveHint(LayerId);
            }
            catch { }
        }

        /// <summary>清空所有缓存文本（回合重置时调用）</summary>
        public void ClearAllCache()
        {
            _lastTexts.Clear();
            _lastSentAt.Clear();
        }

        // ===== 内部 =====

        private string BuildText(Player player)
        {
            var sb = new StringBuilder();
            var cfg = _plugin.Config;

            // 角色介绍（顶部，居中显示）
            sb.Append("<color=#FF69B4>═══ SCP-999 ═══</color>\n");
            sb.Append("<color=#FFE4E1>");
            sb.Append(cfg.Scp999RoleIntro ?? "友好的糖果史莱姆。附近玩家将持续恢复生命，治疗友军。可拾取药品维持活跃状态。");
            sb.Append("</color>\n");

            // 技能 1
            float cd1 = Scp999SkillManager.GetCooldownRemaining(player, 1);
            string t1 = cd1 > 0f ? $"冷却中 <color=#FF4444>{cd1:0}s</color>" : "<color=#00FF00>就绪</color>";
            sb.Append("<color=#FF69B4>[1]</color> 肾上腺素光环  ");
            sb.Append(t1);
            sb.Append("\n");
            sb.Append("<color=#AAAAAA>  附近人类+肾上腺素 ");
            sb.Append($"{cfg.Scp999Skill1Duration:0}s");
            sb.Append(" / 冷却 ");
            sb.Append($"{cfg.Scp999Skill1Cooldown:0}s</color>\n");

            // 技能 2
            float cd2 = Scp999SkillManager.GetCooldownRemaining(player, 2);
            string t2 = cd2 > 0f ? $"冷却中 <color=#FF4444>{cd2:0}s</color>" : "<color=#00FF00>就绪</color>";
            sb.Append("<color=#FF69B4>[2]</color> 范围回复     ");
            sb.Append(t2);
            sb.Append("\n");
            sb.Append("<color=#AAAAAA>  SCP+");
            sb.Append(cfg.Scp999Skill2ScpHeal.ToString("0"));
            sb.Append("/人类+");
            sb.Append(cfg.Scp999Skill2HumanHeal.ToString("0"));
            sb.Append(" 每秒，持续 ");
            sb.Append($"{cfg.Scp999Skill2Duration:0}s");
            sb.Append(" / 冷却 ");
            sb.Append($"{cfg.Scp999Skill2Cooldown:0}s</color>");

            return sb.ToString();
        }

        private void TryShowHint(Player player, string text)
        {
            // HSM 的 ShowHint 同 Id 不替换、不重置计时：每次都是新实例各自到期
            // —— 内容变化时必须先 RemoveHint 同 Id 旧实例再 Show，否则旧 Hint 残留导致文字叠加
            // （与 ExperiencePlugin/ExperienceEventHandler.cs 的 TryShowHint 同一套修复模式）
            string key = $"{player.UserId}|{LayerId}";

            // 关键：hint 只有 10 秒有效期，若内容长期不变（如技能都处于"就绪"）会因跳过重发而永久消失，
            // 所以超过 ResendIntervalSeconds 后即使内容相同也强制重发一次
            bool forceResend = false;
            if (_lastSentAt.TryGetValue(key, out DateTime lastAt) &&
                (DateTime.Now - lastAt).TotalSeconds >= ResendIntervalSeconds)
                forceResend = true;

            if (!forceResend && _lastTexts.TryGetValue(key, out string last) && last == text)
                return; // 内容相同且未到重发间隔，避免闪烁

            _lastTexts[key] = text;
            _lastSentAt[key] = DateTime.Now;

            var disp = PlayerDisplay.Get(player);
            disp.RemoveHint(LayerId);
            disp.ShowHint(new HsmHint
            {
                Id = LayerId,
                Text = text,
                FontSize = _plugin.Config.Scp999HudFontSize,
                YCoordinate = _plugin.Config.Scp999HudYCoordinate,
                Alignment = HintAlignment.Center
            }, 10f);
        }
    }
}