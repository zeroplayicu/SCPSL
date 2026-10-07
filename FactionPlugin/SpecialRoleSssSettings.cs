using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// 特殊角色技能按键（**只有 技能1 / 技能2 两个键**，玩家登录时一次注册，2026-10-02 精简）。
    /// 回调按玩家当前角色自动分发：
    ///   技能1 —— SCP-999：附近人类肾上腺素；GOC：本兵种技能1（抗性/急急急/困住/治疗/隐身/超负荷）
    ///   技能2 —— SCP-999：范围回复；        GOC：本兵种技能2（肾上腺素/激励/震击/左轮）
    /// （奇术师 A7 是常驻武器，鼠标左键直接开火，不占按键）
    /// </summary>
    public class SpecialRoleSssSettings
    {
        // Id 再次变更（配合改名强制刷新客户端绑定，恢复默认键 1 / 2）
        private const int IdHeader = 24030;
        private const int IdSkill1 = 23021;
        private const int IdSkill2 = 23022;

        private readonly FactionPlugin _plugin;
        private readonly List<SettingBase> _settings;

        public SpecialRoleSssSettings(FactionPlugin plugin)
        {
            _plugin = plugin;
            _settings = new List<SettingBase>();
            Build();
        }

        private void Build()
        {
            var header = new HeaderSetting(IdHeader, "特殊角色技能", padding: true);

            var skill1 = new KeybindSetting(IdSkill1, "技能1", KeyCode.Alpha1,
                preventInteractionOnGUI: false, allowSpectatorTrigger: false,
                collectionId: 255, header: header, onChanged: OnSkill1);

            var skill2 = new KeybindSetting(IdSkill2, "技能2", KeyCode.Alpha2,
                preventInteractionOnGUI: false, allowSpectatorTrigger: false,
                collectionId: 255, header: header, onChanged: OnSkill2);

            _settings.Add(skill1);
            _settings.Add(skill2);
        }

        public void SendToPlayer(Player player)
        {
            try
            {
                SettingBase.Register(player, _settings);
            }
            catch (Exception ex)
            {
                Log.Warn($"[特殊角色技能] 发送设置面板失败: {ex.Message}");
            }
        }

        public void RemoveFromPlayer(Player player)
        {
            try { SettingBase.Unregister(player, _settings); } catch { }
        }

        public void UpdateCooldown(Player player)
        {
            // 设置面板无冷却行（冷却由屏幕 HUD 显示）
        }

        public void RemoveFromAll()
        {
            try { SettingBase.Unregister(p => true, _settings); } catch { }
        }

        // ===== 按键回调：按当前角色分发 =====

        private void OnSkill1(Player player, SettingBase setting)
        {
            if (player == null) return;
            if (setting is not KeybindSetting kb) return;
            if (!kb.IsPressed) return;

            if (Scp999Manager.IsScp999(player))
                Scp999SkillManager.TryCastSkill1(player);
            else if (GocManager.IsGoc(player))
                GocSkillManager.TriggerSkill1(player);
            else if (Nu7Manager.IsNu7(player))
                Nu7Manager.TryMedicSkill(player);   // NU7-A 医疗兵：投放补给
        }

        private void OnSkill2(Player player, SettingBase setting)
        {
            if (player == null) return;
            if (setting is not KeybindSetting kb) return;
            if (!kb.IsPressed) return;

            if (Scp999Manager.IsScp999(player))
                Scp999SkillManager.TryCastSkill2(player);
            else if (GocManager.IsGoc(player))
                GocSkillManager.TriggerSkill2(player);
        }
    }
}
