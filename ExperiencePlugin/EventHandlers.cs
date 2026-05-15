using System;
using System.Linq;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;

namespace ExperiencePlugin
{
    public class EventHandlers
    {
        private readonly ExperiencePlugin _plugin;
        public readonly Dictionary<string, CombatData> CombatDataCache = new Dictionary<string, CombatData>();
        private DateTime _roundStartTime;
        private const int BottomPadding = 14;

        public EventHandlers(ExperiencePlugin plugin) { _plugin = plugin; }

        // ==================== 玩家加入 ====================

        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            try { _plugin.DataManager.GetOrCreatePlayerData(ev.Player); }
            catch (Exception ex) { Log.Error($"加入: {ex.Message}"); }
        }

        // ==================== 角色生成 - 发放等级buff（使用MovementBoost代替SCP207，无副作用） ====================

        public void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                int level = data.Level;

                if (player.Role.Side == Side.Scp)
                    ApplyScpBuff(player, level);
                else
                    ApplyHumanBuff(player, level);
            }
            catch (Exception ex) { Log.Error($"生成buff: {ex.Message}"); }
        }

        /// <summary>
        /// SCP等级增幅 - 使用MovementBoost（加速效果，不掉血）
        /// </summary>
        private void ApplyScpBuff(Player player, int level)
        {
            int boost = 0; // MovementBoost intensity

            if (level >= 100) boost = 200;  // 登封 - 超高速
            else if (level >= 50) boost = 120; // 中高速
            else if (level >= 25) boost = 70;  // 中速

            if (boost > 0)
            {
                player.EnableEffect<MovementBoost>((byte)boost, 9999f, false);
                Log.Info($"[Buff] {player.Nickname} 等级{level} → 移速增强×{boost}");
            }
        }

        /// <summary>
        /// 人类阵营buff - 使用MovementBoost
        /// </summary>
        private void ApplyHumanBuff(Player player, int level)
        {
            var role = player.Role.Type;
            int boost = 0;

            // D级人员
            if (role == PlayerRoles.RoleTypeId.ClassD)
            {
                if (level >= 0) player.AddItem(ItemType.KeycardJanitor);
                if (level >= 25) boost = 70;
                else if (level >= 50) boost = 120;
                else if (level >= 100) boost = 200;
            }
            // 博士
            else if (role == PlayerRoles.RoleTypeId.Scientist)
            {
                if (level >= 25) boost = 70;
                else if (level >= 50) boost = 120;
                else if (level >= 100) boost = 200;
            }
            // 保安/MTF
            else if (role == PlayerRoles.RoleTypeId.FacilityGuard ||
                     role == PlayerRoles.RoleTypeId.NtfPrivate ||
                     role == PlayerRoles.RoleTypeId.NtfSergeant ||
                     role == PlayerRoles.RoleTypeId.NtfSpecialist ||
                     role == PlayerRoles.RoleTypeId.NtfCaptain)
            {
                if (level >= 25) boost = 70;
                else if (level >= 50) boost = 120;
                else if (level >= 100) boost = 200;
            }

            if (boost > 0)
            {
                player.EnableEffect<MovementBoost>((byte)boost, 9999f, false);
                Log.Info($"[Buff] {player.Nickname} 等级{level} → 移速增强×{boost}");
            }
        }

        // ==================== 死亡不掉弹药 ====================

        public void OnPlayerDying(DyingEventArgs ev)
        {
            try
            {
                ev.Player.SetAmmo(AmmoType.Nato556, 0);
                ev.Player.SetAmmo(AmmoType.Nato762, 0);
                ev.Player.SetAmmo(AmmoType.Nato9, 0);
            }
            catch (Exception ex) { Log.Error($"Dying: {ex.Message}"); }
        }

        // ==================== 伤害处理（经验累积） ====================

        public void OnPlayerHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                if (ev.Amount <= 0) return;

                int damage = (int)Math.Round(ev.Amount, MidpointRounding.AwayFromZero);
                if (damage <= 0) damage = 1;

                string id = ev.Attacker.UserId;
                if (!CombatDataCache.TryGetValue(id, out CombatData cd))
                {
                    cd = new CombatData();
                    CombatDataCache[id] = cd;
                }
                cd.DamageAccumulated += damage;
                cd.LastHitTime = DateTime.Now;
                cd.IsSettling = false;
                cd.DisplayDamageXp = cd.DamageAccumulated * _plugin.Config.ExpPerDamage;
                cd.LastFeedTime = DateTime.Now;

                RefreshPlayerPanel(ev.Attacker);

                if (_plugin.Config.Debug)
                    Log.Debug($"[伤害] {ev.Attacker.Nickname}: {damage} (累积XP: {cd.DisplayDamageXp})");
            }
            catch (Exception ex) { Log.Error($"伤害事件: {ex.Message}"); }
        }

        // ==================== 无限备弹 ====================

        public void OnReloadingWeapon(ReloadingWeaponEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;
                string itemName = ev.Firearm.Type.ToString();
                if (itemName.Contains("127") || itemName.Contains("SCP127")) return;

                ev.Player.SetAmmo(AmmoType.Nato556, 999);
                ev.Player.SetAmmo(AmmoType.Nato762, 999);
                ev.Player.SetAmmo(AmmoType.Nato9, 999);
            }
            catch (Exception ex) { Log.Error($"换弹: {ex.Message}"); }
        }

        // ==================== 击杀事件 ====================

        public void OnPlayerDied(DiedEventArgs ev)
        {
            try
            {
                string victimId = ev.Player.UserId;
                _plugin.DataManager.AddDeath(victimId);

                // 重置受害者的连杀数（死亡清零）
                if (CombatDataCache.TryGetValue(victimId, out CombatData victimCd))
                {
                    victimCd.KillStreak = 0;
                }

                if (ev.Attacker != null && ev.Attacker != ev.Player)
                {
                    string id = ev.Attacker.UserId;
                    int killExp = _plugin.Config.ExpPerKill;
                    bool leveledUp = _plugin.DataManager.AddExperience(ev.Attacker, killExp);
                    _plugin.DataManager.AddKill(id);

                    if (!CombatDataCache.TryGetValue(id, out CombatData cd))
                    {
                        cd = new CombatData();
                        CombatDataCache[id] = cd;
                    }

                    // 递增连杀数
                    cd.KillStreak++;
                    cd.HasKillExp = true;
                    cd.LastFeedTime = DateTime.Now;
                    RefreshPlayerPanel(ev.Attacker);

                    if (leveledUp)
                    {
                        var data = _plugin.DataManager.GetPlayerData(id);
                        string lvlMsg = "\n\n<size=28><color=lime>升级！{level}</color></size>"
                            .Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
                        ev.Attacker.ShowHint(GetFullHint(id) + lvlMsg, (ushort)(_plugin.Config.StatusRefreshInterval + 2));
                    }
                }
            }
            catch (Exception ex) { Log.Error($"死亡: {ex.Message}"); }
        }

        // ==================== 伤害结算 ====================

        public void CheckAndSettleDamage()
        {
            try
            {
                var now = DateTime.Now;
                var toRemove = new List<string>();
                foreach (var kvp in CombatDataCache)
                {
                    var cd = kvp.Value;
                    if (cd.DamageAccumulated <= 0) { toRemove.Add(kvp.Key); continue; }
                    if (cd.IsSettling) continue;
                    if ((now - cd.LastHitTime).TotalSeconds >= _plugin.Config.DamageSettleDelay)
                    {
                        cd.IsSettling = true;
                        SettleDamageExp(kvp.Key, cd);
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var id in toRemove) CombatDataCache.Remove(id);
            }
            catch (Exception ex) { Log.Error($"结算检查: {ex.Message}"); }
        }

        private void SettleDamageExp(string userId, CombatData cd)
        {
            try
            {
                int damage = cd.DamageAccumulated;
                int expGained = damage * _plugin.Config.ExpPerDamage;
                var player = Player.List.FirstOrDefault(p => p.UserId == userId);
                if (player == null) return;

                bool leveledUp = _plugin.DataManager.AddExperience(player, expGained);
                var data = _plugin.DataManager.GetPlayerData(userId);
                string settle = _plugin.Config.SettleFeedMessage
                    .Replace("{damage}", damage.ToString()).Replace("{exp}", expGained.ToString());
                string status = "\n\n\n\n\n\n\n\n\n\n\n\n\n\n" + FormatStatusLine(data);
                string hint = "\n\n" + settle;
                if (leveledUp)
                    hint += "\n<size=26><color=lime>升级！{level}</color></size>"
                        .Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
                hint += status;
                player.ShowHint(hint, 5);
            }
            catch (Exception ex) { Log.Error($"结算: {ex.Message}"); }
        }

        // ==================== 面板刷新 ====================

        public void RefreshAllStatusPanels()
        {
            try
            {
                if (!_plugin.Config.ShowStatusAlways) return;
                foreach (var player in Player.List.Where(p => p != null))
                    RefreshPlayerPanel(player);
            }
            catch (Exception ex) { Log.Error($"批量刷新: {ex.Message}"); }
        }

        private void RefreshPlayerPanel(Player player)
        {
            try
            {
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;
                player.ShowHint(GetFullHint(player.UserId), (ushort)(_plugin.Config.StatusRefreshInterval + 2));
            }
            catch (Exception ex) { Log.Error($"刷新面板: {ex.Message}"); }
        }

        private string GetFullHint(string userId)
        {
            var data = _plugin.DataManager.GetPlayerData(userId);
            if (data == null) return "";
            string bottomPart = new string('\n', BottomPadding) + FormatStatusLine(data);
            string feed = BuildCombatFeed(userId);
            return string.IsNullOrEmpty(feed) ? bottomPart : feed + bottomPart;
        }

        private string BuildCombatFeed(string userId)
        {
            if (!CombatDataCache.TryGetValue(userId, out CombatData cd)) return "";
            if ((DateTime.Now - cd.LastFeedTime).TotalSeconds > _plugin.Config.FeedDisplayDuration) return "";
            var lines = new List<string>();
            if (cd.DisplayDamageXp > 0)
                lines.Add(_plugin.Config.DamageFeedMessage.Replace("{xp}", cd.DisplayDamageXp.ToString()));
            if (cd.HasKillExp)
                lines.Add(_plugin.Config.KillFeedMessage
                    .Replace("{exp}", _plugin.Config.ExpPerKill.ToString())
                    .Replace("{streak}", cd.KillStreak.ToString()));
            return string.Join("\n", lines);
        }

        private string FormatStatusLine(PlayerData data)
        {
            string msg = _plugin.Config.StatusMessage;
            msg = msg.Replace("{player}", data.PlayerName);
            msg = msg.Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
            msg = msg.Replace("{exp}", data.Experience.ToString());
            msg = msg.Replace("{maxexp}", data.GetExpForNextLevel(_plugin.Config.BaseExpPerLevel).ToString());
            msg = msg.Replace("{time}", data.GetPlayTimeString());
            return msg;
        }

        // ==================== 回合控制 ====================

        public void OnRoundStarted()
        {
            _roundStartTime = DateTime.Now;
            CombatDataCache.Clear();
            Log.Info("回合开始");
        }

        public void OnRoundEnded(RoundEndedEventArgs ev)
        {
            try
            {
                foreach (var kvp in CombatDataCache.ToList())
                {
                    if (kvp.Value.DamageAccumulated > 0 && !kvp.Value.IsSettling)
                    {
                        kvp.Value.IsSettling = true;
                        SettleDamageExp(kvp.Key, kvp.Value);
                    }
                }
                CombatDataCache.Clear();

                int minutes = (int)(DateTime.Now - _roundStartTime).TotalMinutes;
                if (minutes > 0)
                {
                    foreach (var player in Player.List)
                    {
                        _plugin.DataManager.UpdatePlayTime(player.UserId, minutes);
                        int exp = minutes * _plugin.Config.ExpPerMinute;
                        if (exp > 0) _plugin.DataManager.AddExperience(player, exp);
                    }
                }
                _plugin.DataManager.SaveAllData();
            }
            catch (Exception ex) { Log.Error($"回合结束: {ex.Message}"); }
        }
    }
}
