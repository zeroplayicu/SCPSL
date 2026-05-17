using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Events.Handlers;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using PlayerRoles;

namespace CommanderShieldPlugin
{
    public class ShieldEventHandler : CustomEventsHandler
    {
        // 使用 UserId+PlayerId 双键隔离，防止同名玩家数据错乱
        private readonly Dictionary<string, ShieldData> _shieldData = new Dictionary<string, ShieldData>();

        // 攻击累积HS: UserId -> (总伤害, 目标是否SCP)
        private readonly Dictionary<string, AttackTracking> _attackTracking = new Dictionary<string, AttackTracking>();

        private class ShieldData
        {
            public int CurrentAHP { get; set; }
            public int CurrentHS { get; set; }
            public int MaxAHP { get; set; }
            public int MaxHS { get; set; }
        }

        private class AttackTracking
        {
            public int TotalDamage { get; set; }
            public bool IsHittingSCP { get; set; }
        }

        public void RegisterRoundEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
        }

        public void UnregisterRoundEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
        }

        /// <summary>
        /// 将 AHP 同步到游戏内置护盾（右下角蓝条）
        /// 必须先设 Max，再设 value，否则蓝条不显示
        /// </summary>
        private static void SyncAhpToGame(Player player, ShieldData sd)
        {
            if (sd.CurrentAHP <= 0)
            {
                player.ArtificialHealth = 0;
                return;
            }
            player.MaxArtificialHealth = sd.MaxAHP;
            player.ArtificialHealth = sd.CurrentAHP;
        }

        /// <summary>
        /// 获取护盾数据（检查UserId匹配，确保玩家身份唯一）
        /// </summary>
        private bool TryGetShield(Player player, out ShieldData sd)
        {
            return _shieldData.TryGetValue(player.UserId, out sd);
        }

        // ==================== NTF指挥官生成 - 激活护盾 ====================

        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;

                if (player.Role != RoleTypeId.NtfCaptain)
                {
                    if (_shieldData.Remove(player.UserId))
                    {
                        player.ArtificialHealth = 0;
                        Logger.Debug($"[护盾] {player.Nickname} 切换为非指挥官 → 护盾已移除");
                    }
                    return;
                }

                // NTF指挥官生成 - 创建/重置护盾
                if (_shieldData.TryGetValue(player.UserId, out var existing))
                {
                    existing.CurrentAHP = existing.MaxAHP;
                    existing.CurrentHS = existing.MaxHS;
                    Logger.Info($"[护盾] {player.Nickname} 重新生成 → 护盾重置满值");
                }
                else
                {
                    var cfg = CommanderShieldPlugin.Instance.Config;
                    var sd = new ShieldData
                    {
                        MaxAHP = cfg.MaxShieldAHP,
                        MaxHS = cfg.MaxShieldHS,
                        CurrentAHP = cfg.MaxShieldAHP,
                        CurrentHS = cfg.MaxShieldHS
                    };
                    _shieldData[player.UserId] = sd;
                    Logger.Info($"[护盾] {player.Nickname} 成为NTF指挥官 → 量子护盾激活 (AHP:{sd.CurrentAHP} HS:{sd.CurrentHS})");
                }

                SyncAhpToGame(player, _shieldData[player.UserId]);

                if (CommanderShieldPlugin.Instance.Config.ReplaceCommanderCard)
                {
                    foreach (var item in player.Items.ToList())
                    {
                        if (item.Type == ItemType.KeycardMTFCaptain)
                            player.RemoveItem(item);
                    }
                    player.AddItem(ItemType.KeycardO5);
                }

                // 指挥官备弹设为101
                try
                {
                    foreach (var ammoType in player.Ammo.Keys.ToList())
                        player.SetAmmo(ammoType, 101);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Logger.Error($"指挥官生成错误: {ex.Message}");
            }
        }

        // ==================== 攻击获得HS护盾 ====================

        public override void OnPlayerHurt(PlayerHurtEventArgs ev)
        {
            try
            {
                // 攻击者是指挥官且有护盾
                if (ev.Attacker == null || ev.Player == null) return;
                if (!_shieldData.ContainsKey(ev.Attacker.UserId)) return;

                float amount = 0f;
                var dmgProp = ev.DamageHandler.GetType().GetProperty("Damage");
                if (dmgProp != null)
                    amount = (float)dmgProp.GetValue(ev.DamageHandler);
                if (amount <= 0) return;

                int damage = (int)Math.Round(amount);
                if (damage <= 0) return;

                bool isScp = ev.Player.IsSCP;

                // 累积攻击伤害
                string key = ev.Attacker.UserId;
                if (!_attackTracking.TryGetValue(key, out var at))
                {
                    at = new AttackTracking();
                    _attackTracking[key] = at;
                }
                at.TotalDamage += damage;

                // 计算应该获得的HS数量
                int threshold = isScp ? 30 : 10;
                int hsGained = at.TotalDamage / threshold;
                int hsToAdd = hsGained;

                if (hsToAdd > 0)
                {
                    // 重置计数（只计算新获得的）
                    at.TotalDamage = at.TotalDamage % threshold;

                    if (_shieldData.TryGetValue(key, out var sd))
                    {
                        sd.CurrentHS = Math.Min(sd.CurrentHS + hsToAdd, sd.MaxHS);
                        // 不输出控制台日志（攻击频繁，太耗费资源）
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"攻击护盾错误: {ex.Message}");
            }
        }

        // ==================== 伤害处理（护盾吸收） ====================

        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                if (!_shieldData.TryGetValue(ev.Player.UserId, out ShieldData sd)) return;
                if (sd.CurrentAHP <= 0 && sd.CurrentHS <= 0) return;

                float damage = 0f;
                var dmgProp = ev.DamageHandler.GetType().GetProperty("Damage");
                if (dmgProp != null)
                    damage = (float)dmgProp.GetValue(ev.DamageHandler);
                if (damage <= 0) return;

                float originalDamage = damage;

                if (sd.CurrentAHP > 0 && damage > 0)
                {
                    int absorbed = (int)Math.Min(damage, sd.CurrentAHP);
                    sd.CurrentAHP -= absorbed;
                    damage -= absorbed;
                }

                if (damage > 0 && sd.CurrentHS > 0)
                {
                    int absorbed = (int)Math.Min(damage, sd.CurrentHS);
                    sd.CurrentHS -= absorbed;
                    damage -= absorbed;
                }

                var dmgSetProp = ev.DamageHandler.GetType().GetProperty("Damage");
                if (dmgSetProp != null && dmgSetProp.CanWrite)
                    dmgSetProp.SetValue(ev.DamageHandler, damage);

                SyncAhpToGame(ev.Player, sd);

                if (damage < originalDamage)
                    Logger.Debug($"[护盾] {ev.Player.Nickname} 吸收 {(int)(originalDamage - damage)} 伤害 → AHP:{sd.CurrentAHP} HS:{sd.CurrentHS}");
            }
            catch (Exception ex)
            {
                Logger.Error($"护盾伤害错误: {ex.Message}");
            }
        }

        // ==================== 指挥官死亡 - 移除护盾 ====================

        public override void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            try
            {
                if (_shieldData.Remove(ev.Player.UserId))
                {
                    ev.Player.ArtificialHealth = 0;
                }
                _attackTracking.Remove(ev.Player.UserId);
            }
            catch (Exception ex)
            {
                Logger.Error($"护盾死亡清理错误: {ex.Message}");
            }
        }

        // ==================== 护盾再生（每秒） ====================

        public void RegenerateShields()
        {
            try
            {
                var cfg = CommanderShieldPlugin.Instance.Config;
                if (cfg.RegenPerTick <= 0) return;

                foreach (var kvp in _shieldData.ToList())
                {
                    ShieldData sd = kvp.Value;
                    bool changed = false;

                    if (sd.CurrentAHP < sd.MaxAHP)
                    {
                        sd.CurrentAHP = Math.Min(sd.CurrentAHP + cfg.RegenPerTick, sd.MaxAHP);
                        changed = true;
                    }
                    if (sd.CurrentHS < sd.MaxHS)
                    {
                        sd.CurrentHS = Math.Min(sd.CurrentHS + cfg.RegenPerTick, sd.MaxHS);
                        changed = true;
                    }

                    if (changed)
                    {
                        var player = Player.List.FirstOrDefault(p => p != null && p.UserId == kvp.Key);
                        if (player != null)
                            SyncAhpToGame(player, sd);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"护盾再生错误: {ex.Message}");
            }
        }

        // ==================== HUD 显示（改用Broadcast避免与经验插件Hint冲突） ====================

        public void RefreshAllHuds()
        {
            try
            {
                foreach (var player in Player.List.Where(p => p != null))
                {
                    if (!TryGetShield(player, out ShieldData sd)) continue;
                    if (sd.CurrentAHP <= 0 && sd.CurrentHS <= 0) continue;

                    string hud = BuildHudString(sd);
                    // 改用SendBroadcast替代SendHint，避免与ExperiencePlugin的Hint冲突闪烁
                    player.ClearBroadcasts();
                    player.SendBroadcast(hud, (ushort)(CommanderShieldPlugin.Instance.Config.HudRefreshInterval + 1));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"HUD刷新错误: {ex.Message}");
            }
        }

        private static string BuildHudString(ShieldData sd)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=18><color=#00BFFF>═══ 量子护盾 (NTF指挥官) ═══</color></size>");
            sb.AppendLine(BuildBar("AHP", sd.CurrentAHP, sd.MaxAHP));
            sb.AppendLine(BuildBar("HS ", sd.CurrentHS, sd.MaxHS));
            sb.AppendLine(
                $"<size=14><color=#00BFFF>AHP:</color><color=white>{sd.CurrentAHP}/{sd.MaxAHP}</color>" +
                $"   <color=#00FFFF>HS:</color><color=white>{sd.CurrentHS}/{sd.MaxHS}</color></size>");
            return sb.ToString();
        }

        private static string BuildBar(string label, int current, int max)
        {
            if (max <= 0) return "";
            float ratio = (float)current / max;
            int totalBlocks = 10;
            int filled = (int)Math.Round(ratio * totalBlocks);

            string colorTag;
            if (ratio > 0.5f) colorTag = "#00FF00";
            else if (ratio > 0.25f) colorTag = "#FFA500";
            else colorTag = "#FF4444";

            string bar = new string('█', filled) + new string('░', totalBlocks - filled);
            return $"<size=14><color=#AAAAAA>{label}</color> <color={colorTag}>{bar}</color></size>";
        }

        // ==================== 回合开始清理 ====================

        private void OnRoundStarted()
        {
            _shieldData.Clear();
            _attackTracking.Clear();
            Logger.Info("[护盾] 新回合开始 → 所有数据已清空");
        }

        // ==================== 插件卸载清理 ====================

        public void ClearAll()
        {
            foreach (var kvp in _shieldData)
            {
                var player = Player.List.FirstOrDefault(p => p != null && p.UserId == kvp.Key);
                if (player != null)
                    player.ArtificialHealth = 0;
            }
            _shieldData.Clear();
            _attackTracking.Clear();
        }
    }
}
