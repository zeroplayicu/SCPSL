using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;

namespace CommanderShieldPlugin
{
    public class EventHandlers
    {
        // 护盾数据: UserId -> { CurrentAHP, CurrentHS, MaxAHP, MaxHS }
        private readonly Dictionary<string, ShieldData> _shieldData = new Dictionary<string, ShieldData>();

        // ==================== 指挥官生成 ====================

        public void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;

                // 只处理 NTF 指挥官
                if (player.Role.Type != RoleTypeId.NtfCaptain) return;

                var cfg = CommanderShieldPlugin.Instance.Config;

                // 创建护盾数据
                var sd = new ShieldData
                {
                    MaxAHP = cfg.MaxShieldAHP,
                    MaxHS = cfg.MaxShieldHS,
                    CurrentAHP = cfg.MaxShieldAHP,
                    CurrentHS = cfg.MaxShieldHS
                };
                _shieldData[player.UserId] = sd;

                // 替换指挥官卡为 O5 权限卡
                if (cfg.ReplaceCommanderCard)
                {
                    // 移除所有已有卡，添加 O5 卡
                    foreach (var item in player.Items.ToList())
                    {
                        if (item.Type == ItemType.KeycardMTFCaptain)
                            item.Destroy();
                    }
                    player.AddItem(ItemType.KeycardO5);
                }

                Log.Info($"[护盾] {player.Nickname} 成为NTF指挥官 → 量子护盾激活 (AHP:{sd.CurrentAHP} HS:{sd.CurrentHS})");
            }
            catch (Exception ex)
            {
                Log.Error($"指挥官生成错误: {ex.Message}");
            }
        }

        // ==================== 伤害处理（护盾吸收） ====================

        public void OnPlayerHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                string userId = ev.Player.UserId;

                if (!_shieldData.TryGetValue(userId, out ShieldData sd)) return;
                if (sd.CurrentAHP <= 0 && sd.CurrentHS <= 0) return;

                float damage = ev.Amount;
                if (damage <= 0) return;

                // AHP 优先吸收伤害
                if (sd.CurrentAHP > 0)
                {
                    int absorbed = (int)Math.Min(damage, sd.CurrentAHP);
                    sd.CurrentAHP -= absorbed;
                    damage -= absorbed;
                }

                // HS 次级吸收
                if (damage > 0 && sd.CurrentHS > 0)
                {
                    int absorbed = (int)Math.Min(damage, sd.CurrentHS);
                    sd.CurrentHS -= absorbed;
                    damage -= absorbed;
                }

                // 设置最终伤害
                ev.Amount = damage;

                if (CommanderShieldPlugin.Instance.Config.Debug)
                    Log.Debug($"[护盾] {ev.Player.Nickname} 吸收{ev.Amount}伤害 → AHP:{sd.CurrentAHP} HS:{sd.CurrentHS}");
            }
            catch (Exception ex)
            {
                Log.Error($"护盾伤害错误: {ex.Message}");
            }
        }

        // ==================== HUD 显示 ====================

        public void RefreshAllHuds()
        {
            try
            {
                var cfg = CommanderShieldPlugin.Instance.Config;

                foreach (var player in Player.List.Where(p => p != null))
                {
                    if (!_shieldData.TryGetValue(player.UserId, out ShieldData sd)) continue;
                    if (sd.CurrentAHP <= 0 && sd.CurrentHS <= 0) continue;

                    string hud = BuildHudString(sd, cfg);
                    player.ShowHint(hud, (ushort)(cfg.HudRefreshInterval + 1));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HUD刷新错误: {ex.Message}");
            }
        }

        private static string BuildHudString(ShieldData sd, CommanderShieldConfig cfg)
        {
            // 用新行将HUD推到准星右侧区域（屏幕中间偏右位置）
            string padding = new string('\n', 9);
            string content = cfg.HudContentTemplate
                .Replace("{ahp}", sd.CurrentAHP.ToString())
                .Replace("{hs}", sd.CurrentHS.ToString());

            return $"{padding}" +
                   $"<size=18><color=#00BFFF>{cfg.HudTitle}</color></size>\n" +
                   $"<size=16><color=#FFD700>{content}</color></size>\n" +
                   $"<size=18><color=#00BFFF>{cfg.HudFooter}</color></size>";
        }

        // ==================== 清理 ====================

        public void ClearAll()
        {
            _shieldData.Clear();
        }

        private class ShieldData
        {
            public int CurrentAHP { get; set; }
            public int CurrentHS { get; set; }
            public int MaxAHP { get; set; }
            public int MaxHS { get; set; }
        }
    }
}
