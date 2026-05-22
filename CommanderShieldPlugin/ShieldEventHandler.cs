using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;

namespace CommanderShieldPlugin
{
    public class ShieldEventHandler
    {
        private readonly Dictionary<string, ShieldData> _shieldData = new Dictionary<string, ShieldData>();
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
        }

        private static bool IsValidPlayer(Player p)
        {
            return p != null && !p.IsNpc && !string.IsNullOrEmpty(p.UserId);
        }

        private static void SyncAhpToGame(Player player, ShieldData sd)
        {
            if (sd.CurrentAHP <= 0) { player.ArtificialHealth = 0; return; }
            player.MaxArtificialHealth = sd.MaxAHP;
            player.ArtificialHealth = sd.CurrentAHP;
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                if (!IsValidPlayer(player)) return;

                if (player.Role.Type != RoleTypeId.NtfCaptain)
                {
                    if (_shieldData.Remove(player.UserId))
                        player.ArtificialHealth = 0;
                    return;
                }

                if (_shieldData.TryGetValue(player.UserId, out var existing))
                {
                    existing.CurrentAHP = existing.MaxAHP;
                    existing.CurrentHS = existing.MaxHS;
                }
                else
                {
                    var cfg = CommanderShieldPlugin.Instance.Config;
                    _shieldData[player.UserId] = new ShieldData
                    {
                        MaxAHP = cfg.MaxShieldAHP, MaxHS = cfg.MaxShieldHS,
                        CurrentAHP = cfg.MaxShieldAHP, CurrentHS = cfg.MaxShieldHS
                    };
                }

                SyncAhpToGame(player, _shieldData[player.UserId]);

                if (CommanderShieldPlugin.Instance.Config.ReplaceCommanderCard)
                {
                    foreach (var item in player.Items.ToList())
                        if (item.Type == ItemType.KeycardMTFCaptain)
                            player.RemoveItem(item);
                    player.AddItem(ItemType.KeycardO5);
                }

                try
                {
                    foreach (var at in player.Ammo.Keys.ToList())
                        player.AddAmmo(at, 101);
                }
                catch { }
            }
            catch (Exception ex) { Log.Error($"指挥官生成错误: {ex.Message}"); }
        }

        public void OnHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (!IsValidPlayer(ev.Attacker)) return;
                if (!_shieldData.ContainsKey(ev.Attacker.UserId)) return;

                float amount = ev.Amount;
                if (amount <= 0) return;

                int damage = (int)Math.Round(amount);
                if (damage <= 0) return;

                bool isScp = ev.Player.IsScp;
                string key = ev.Attacker.UserId;

                if (!_attackTracking.TryGetValue(key, out var at))
                {
                    at = new AttackTracking();
                    _attackTracking[key] = at;
                }
                at.TotalDamage += damage;

                int threshold = isScp ? 30 : 10;
                int hsToAdd = at.TotalDamage / threshold;

                if (hsToAdd > 0)
                {
                    at.TotalDamage = at.TotalDamage % threshold;
                    if (_shieldData.TryGetValue(key, out var sd))
                        sd.CurrentHS = Math.Min(sd.CurrentHS + hsToAdd, sd.MaxHS);
                }
            }
            catch { }
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                if (!_shieldData.TryGetValue(ev.Player.UserId, out ShieldData sd)) return;
                if (sd.CurrentAHP <= 0 && sd.CurrentHS <= 0) return;

                float damage = ev.Amount;
                if (damage <= 0) return;

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

                ev.Amount = damage;
                SyncAhpToGame(ev.Player, sd);
            }
            catch { }
        }

        public void OnDied(DiedEventArgs ev)
        {
            try
            {
                if (!IsValidPlayer(ev.Target)) return;
                if (_shieldData.Remove(ev.Target.UserId))
                    ev.Target.ArtificialHealth = 0;
                _attackTracking.Remove(ev.Target.UserId);
            }
            catch { }
        }

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
                    if (sd.CurrentAHP < sd.MaxAHP) { sd.CurrentAHP = Math.Min(sd.CurrentAHP + cfg.RegenPerTick, sd.MaxAHP); changed = true; }
                    if (sd.CurrentHS < sd.MaxHS) { sd.CurrentHS = Math.Min(sd.CurrentHS + cfg.RegenPerTick, sd.MaxHS); changed = true; }
                    if (changed)
                    {
                        var player = Player.List.FirstOrDefault(p => IsValidPlayer(p) && p.UserId == kvp.Key);
                        if (player != null) SyncAhpToGame(player, sd);
                    }
                }
            }
            catch { }
        }

        public void RefreshAllHuds()
        {
            try
            {
                foreach (var player in Player.List.Where(p => IsValidPlayer(p)))
                {
                    if (!_shieldData.TryGetValue(player.UserId, out ShieldData sd)) continue;
                    if (sd.CurrentAHP <= 0 && sd.CurrentHS <= 0) continue;

                    var hud = BuildHudString(sd);
                    player.ShowHint(hud, (ushort)(CommanderShieldPlugin.Instance.Config.HudRefreshInterval + 1));
                }
            }
            catch { }
        }

        private static string BuildHudString(ShieldData sd)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=18><color=#00BFFF>═══ 量子护盾 ═══</color></size>");
            sb.AppendLine(BuildBar("AHP", sd.CurrentAHP, sd.MaxAHP));
            sb.AppendLine(BuildBar("HS ", sd.CurrentHS, sd.MaxHS));
            sb.AppendLine($"<size=14><color=#00BFFF>AHP:</color><color=white>{sd.CurrentAHP}/{sd.MaxAHP}</color>   <color=#00FFFF>HS:</color><color=white>{sd.CurrentHS}/{sd.MaxHS}</color></size>");
            return sb.ToString();
        }

        private static string BuildBar(string label, int current, int max)
        {
            if (max <= 0) return "";
            float ratio = (float)current / max;
            int filled = (int)Math.Round(ratio * 10);
            string color = ratio > 0.5f ? "#00FF00" : ratio > 0.25f ? "#FFA500" : "#FF4444";
            return $"<size=14><color=#AAAAAA>{label}</color> <color={color}>{new string('█', filled)}{new string('░', 10 - filled)}</color></size>";
        }

        public void OnRoundStarted() { _shieldData.Clear(); _attackTracking.Clear(); }

        public void ClearAll()
        {
            foreach (var kvp in _shieldData)
            {
                var p = Player.List.FirstOrDefault(x => IsValidPlayer(x) && x.UserId == kvp.Key);
                if (p != null) p.ArtificialHealth = 0;
            }
            _shieldData.Clear(); _attackTracking.Clear();
        }
    }
}
