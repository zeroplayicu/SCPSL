using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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

        // ===== 本局战绩 KDA =====
        private readonly Dictionary<string, int> _roundKills = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundDeaths = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundAssists = new Dictionary<string, int>();

        // ===== 助攻伤害追踪: key=受害者UserId, value={攻击者UserId -> 总伤害}
        private readonly Dictionary<string, Dictionary<string, int>> _assistDamage = new Dictionary<string, Dictionary<string, int>>();

        // ===== 指挥官护盾数据 =====
        private readonly Dictionary<string, ShieldData> _commanderShields = new Dictionary<string, ShieldData>();

        private class ShieldData
        {
            public int AHP { get; set; }
            public int HS { get; set; }
            public int MaxAHP { get; set; }
            public int MaxHS { get; set; }
        }

        public EventHandlers(ExperiencePlugin plugin) { _plugin = plugin; }

        // ==================== 玩家加入 ====================

        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            try { _plugin.DataManager.GetOrCreatePlayerData(ev.Player); }
            catch (Exception ex) { Log.Error($"加入: {ex.Message}"); }
        }

        // ==================== 角色生成 - 发放等级buff ====================

        public void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                int level = data.Level;

                if (player.Role.Side == Side.Scp)
                    ApplyScpBuff(player, level);
                else
                    ApplyHumanBuff(player, level);

                // === 护盾清理（非指挥官时移除） + NTF 指挥官护盾激活 ===
                if (_commanderShields.ContainsKey(player.UserId) && player.Role.Type != RoleTypeId.NtfCaptain)
                {
                    _commanderShields.Remove(player.UserId);
                    player.ArtificialHealth = 0;
                }

                if (player.Role.Type == RoleTypeId.NtfCaptain && _plugin.Config.EnableCommanderShield)
                {
                    var sd = new ShieldData
                    {
                        MaxAHP = _plugin.Config.CommanderMaxAHP,
                        MaxHS = _plugin.Config.CommanderMaxHS,
                        AHP = _plugin.Config.CommanderMaxAHP,
                        HS = _plugin.Config.CommanderMaxHS
                    };
                    _commanderShields[player.UserId] = sd;

                    // 写入游戏内置人工生命值 → 右下角血量旁显示蓝条
                    player.ArtificialHealth = sd.AHP;
                    player.MaxArtificialHealth = sd.MaxAHP;

                    // 替换指挥官卡为 O5 权限卡
                    if (_plugin.Config.ReplaceCommanderCard)
                    {
                        foreach (var item in player.Items.ToList())
                        {
                            if (item.Type == ItemType.KeycardMTFCaptain)
                                item.Destroy();
                        }
                        player.AddItem(ItemType.KeycardO5);
                    }

                    Log.Info($"[护盾] {player.Nickname} 指挥官护盾激活 (AHP:{sd.AHP} HS:{sd.HS})");
                }
            }
            catch (Exception ex) { Log.Error($"生成buff: {ex.Message}"); }
        }

        private void ApplyScpBuff(Player player, int level)
        {
            int boost = 0;
            if (level >= 100) boost = 3;
            else if (level >= 50) boost = 2;
            else if (level >= 25) boost = 1;

            if (boost > 0)
            {
                player.EnableEffect<Scp207>((byte)boost, 9999f, false);
                Log.Info($"[Buff] {player.Nickname} 等级{level} → SCP207×{boost}");
            }
        }

        private void ApplyHumanBuff(Player player, int level)
        {
            var role = player.Role.Type;
            int boost = 0;

            if (role == PlayerRoles.RoleTypeId.ClassD)
            {
                if (level >= 0) player.AddItem(ItemType.KeycardJanitor);
                if (level >= 100) boost = 3;
                else if (level >= 50) boost = 2;
                else if (level >= 25) boost = 1;
            }
            else if (role == PlayerRoles.RoleTypeId.Scientist)
            {
                if (level >= 100) boost = 3;
                else if (level >= 50) boost = 2;
                else if (level >= 25) boost = 1;
            }
            else if (role == PlayerRoles.RoleTypeId.FacilityGuard ||
                     role == PlayerRoles.RoleTypeId.NtfPrivate ||
                     role == PlayerRoles.RoleTypeId.NtfSergeant ||
                     role == PlayerRoles.RoleTypeId.NtfSpecialist ||
                     role == PlayerRoles.RoleTypeId.NtfCaptain)
            {
                if (level >= 100) boost = 3;
                else if (level >= 50) boost = 2;
                else if (level >= 25) boost = 1;
            }

            if (boost > 0)
            {
                player.EnableEffect<Scp207>((byte)boost, 9999f, false);
                Log.Info($"[Buff] {player.Nickname} 等级{level} → SCP207×{boost}");
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
                ev.Player.SetAmmo(AmmoType.Ammo12Gauge, 0);
            }
            catch (Exception ex) { Log.Error($"Dying: {ex.Message}"); }
        }

        // ==================== 伤害处理（经验累积 + 助攻追踪） ====================

        public void OnPlayerHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                if (ev.Amount <= 0) return;

                int damage = (int)Math.Round(ev.Amount, MidpointRounding.AwayFromZero);
                if (damage <= 0) damage = 1;

                string attackerId = ev.Attacker.UserId;
                string victimId = ev.Player.UserId;

                // === 1. 伤害经验累积（原有逻辑） ===
                if (!CombatDataCache.TryGetValue(attackerId, out CombatData cd))
                {
                    cd = new CombatData();
                    CombatDataCache[attackerId] = cd;
                }
                cd.DamageAccumulated += damage;
                cd.LastHitTime = DateTime.Now;
                cd.IsSettling = false;
                cd.DisplayDamageXp = cd.DamageAccumulated * _plugin.Config.ExpPerDamage;
                cd.LastFeedTime = DateTime.Now;

                RefreshPlayerPanel(ev.Attacker);

                // === 2. 助攻伤害追踪 ===
                if (!_assistDamage.TryGetValue(victimId, out var attackerDict))
                {
                    attackerDict = new Dictionary<string, int>();
                    _assistDamage[victimId] = attackerDict;
                }
                if (!attackerDict.TryGetValue(attackerId, out int prevDamage))
                    prevDamage = 0;
                attackerDict[attackerId] = prevDamage + damage;

                if (_plugin.Config.Debug)
                    Log.Debug($"[伤害] {ev.Attacker.Nickname}: {damage} (累积XP: {cd.DisplayDamageXp})");
            }
            catch (Exception ex) { Log.Error($"伤害事件: {ex.Message}"); }
        }

        // ==================== SCP207 无伤 + 指挥官护盾吸伤 ====================

        public void OnPlayerHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;

                // SCP207 无伤害
                if (_plugin.Config.Scp207NoDrain && ev.DamageHandler.Type == DamageType.Scp207)
                {
                    ev.IsAllowed = false;
                    return;
                }

                // 指挥官护盾吸伤
                string userId = ev.Player.UserId;
                if (_commanderShields.TryGetValue(userId, out ShieldData sd))
                {
                    if (sd.AHP <= 0 && sd.HS <= 0) return;

                    float damage = ev.Amount;
                    if (damage <= 0) return;

                    // AHP 优先吸收
                    if (sd.AHP > 0 && damage > 0)
                    {
                        int absorbed = (int)Math.Min(damage, sd.AHP);
                        sd.AHP -= absorbed;
                        damage -= absorbed;
                    }
                    // HS 次级吸收
                    if (sd.HS > 0 && damage > 0)
                    {
                        int absorbed = (int)Math.Min(damage, sd.HS);
                        sd.HS -= absorbed;
                        damage -= absorbed;
                    }

                    ev.Amount = damage;

                    if (_plugin.Config.Debug)
                        Log.Debug($"[护盾] {ev.Player.Nickname} 吸收伤害 → AHP:{sd.AHP} HS:{sd.HS}");
                }
            }
            catch (Exception ex) { Log.Error($"Hurting处理: {ex.Message}"); }
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
                ev.Player.SetAmmo(AmmoType.Ammo12Gauge, 999);
            }
            catch (Exception ex) { Log.Error($"换弹: {ex.Message}"); }
        }

        // ==================== 丢枪清零弹药（防止掉落子弹） ====================

        public void OnDroppingItem(DroppingItemEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;
                // 丢枪前清零备弹（含霰弹），防止生成子弹掉落物
                ev.Player.SetAmmo(AmmoType.Nato556, 0);
                ev.Player.SetAmmo(AmmoType.Nato762, 0);
                ev.Player.SetAmmo(AmmoType.Nato9, 0);
                ev.Player.SetAmmo(AmmoType.Ammo12Gauge, 0);
            }
            catch (Exception ex) { Log.Error($"丢枪: {ex.Message}"); }
        }

        // ==================== 击杀事件 + 助攻结算 ====================

        public void OnPlayerDied(DiedEventArgs ev)
        {
            try
            {
                string victimId = ev.Player.UserId;
                _plugin.DataManager.AddDeath(victimId);
                AddRoundDeath(victimId);

                // 指挥官死亡 → 移除护盾
                if (_commanderShields.Remove(victimId))
                    ev.Player.ArtificialHealth = 0;

                // 重置受害者连杀数
                if (CombatDataCache.TryGetValue(victimId, out CombatData victimCd))
                    victimCd.KillStreak = 0;

                // --- 攻击者击杀处理 ---
                if (ev.Attacker != null && ev.Attacker != ev.Player)
                {
                    string killerId = ev.Attacker.UserId;
                    int killExp = _plugin.Config.ExpPerKill;
                    bool leveledUp = _plugin.DataManager.AddExperience(ev.Attacker, killExp);
                    _plugin.DataManager.AddKill(killerId);
                    AddRoundKill(killerId);

                    if (!CombatDataCache.TryGetValue(killerId, out CombatData cd))
                    {
                        cd = new CombatData();
                        CombatDataCache[killerId] = cd;
                    }
                    cd.KillStreak++;
                    cd.HasKillExp = true;
                    cd.LastFeedTime = DateTime.Now;
                    RefreshPlayerPanel(ev.Attacker);

                    if (leveledUp)
                    {
                        var data = _plugin.DataManager.GetPlayerData(killerId);
                        string lvlMsg = "\n\n<size=28><color=lime>升级！{level}</color></size>"
                            .Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
                        ev.Attacker.ShowHint(GetFullHint(killerId) + lvlMsg, (ushort)(_plugin.Config.StatusRefreshInterval + 2));
                    }
                }

                // --- 助攻结算 ---
                ProcessAssists(ev);
            }
            catch (Exception ex) { Log.Error($"死亡: {ex.Message}"); }
        }

        /// <summary>
        /// 处理助攻逻辑
        /// </summary>
        private void ProcessAssists(DiedEventArgs ev)
        {
            string victimId = ev.Player.UserId;

            // 检查是否有对受害者造成伤害的记录
            if (!_assistDamage.TryGetValue(victimId, out var attackerDict)) return;

            bool victimIsScp = ev.Player.Role.Side == Side.Scp;
            string killerId = (ev.Attacker != null && ev.Attacker != ev.Player) ? ev.Attacker.UserId : null;

            foreach (var kvp in attackerDict)
            {
                string attackerId = kvp.Key;
                int damageDealt = kvp.Value;

                // 跳过击杀者本人（击杀已有经验，不重复给助攻）
                if (attackerId == killerId) continue;

                bool giveAssist = false;
                int assistExp = 0;

                if (victimIsScp)
                {
                    // SCP助攻：攻击250血以上
                    if (damageDealt >= _plugin.Config.ScpAssistThreshold)
                        giveAssist = true;
                }
                else
                {
                    // 人类助攻：攻击20血以上，按1血5xp
                    if (damageDealt >= _plugin.Config.HumanAssistThreshold)
                    {
                        giveAssist = true;
                        assistExp = damageDealt * _plugin.Config.HumanAssistExpPerDamage;
                    }
                }

                if (giveAssist)
                {
                    var assister = Player.List.FirstOrDefault(p => p.UserId == attackerId);
                    if (assister != null)
                    {
                        AddRoundAssist(attackerId);

                        if (assistExp > 0)
                        {
                            bool leveledUp = _plugin.DataManager.AddExperience(assister, assistExp);
                            if (_plugin.Config.Debug)
                                Log.Debug($"[助攻] {assister.Nickname}: 助攻{damageDealt}伤害 → +{assistExp}xp" +
                                    (leveledUp ? " (升级!)" : ""));
                        }

                        if (_plugin.Config.Debug)
                            Log.Debug($"[助攻] {assister.Nickname}: 助攻 ({damageDealt}伤害)");
                    }
                }
            }

            // 清理已处理的受害者记录
            _assistDamage.Remove(victimId);
        }

        // ==================== 本局 KDA 统计 ====================

        private void AddRoundKill(string userId)
        {
            if (!_roundKills.ContainsKey(userId)) _roundKills[userId] = 0;
            _roundKills[userId]++;
        }

        private void AddRoundDeath(string userId)
        {
            if (!_roundDeaths.ContainsKey(userId)) _roundDeaths[userId] = 0;
            _roundDeaths[userId]++;
        }

        private void AddRoundAssist(string userId)
        {
            if (!_roundAssists.ContainsKey(userId)) _roundAssists[userId] = 0;
            _roundAssists[userId]++;
        }

        private string GetRoundKDAString(string userId)
        {
            int k = _roundKills.TryGetValue(userId, out int kv) ? kv : 0;
            int d = _roundDeaths.TryGetValue(userId, out int dv) ? dv : 0;
            int a = _roundAssists.TryGetValue(userId, out int av) ? av : 0;
            return $"{k}/{d}/{a}";
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
                string status = "\n\n\n\n\n\n\n\n\n\n\n\n\n\n" + FormatStatusLine(data, userId);
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

            var parts = new List<string>();

            // 战斗反馈（伤害/击杀提示）
            string feed = BuildCombatFeed(userId);
            if (!string.IsNullOrEmpty(feed))
                parts.Add(feed);

            // 效果常驻显示（类似 EffectDisplay 的自动HUD）
            string effects = BuildEffectsDisplay(userId);
            if (!string.IsNullOrEmpty(effects))
                parts.Add(effects);

            // 底部状态栏（经验 + KDA）
            string status = FormatStatusLine(data, userId);
            parts.Add(status);

            return string.Join("\n\n\n\n", parts);
        }

        // ==================== 效果常驻显示（反射版，借鉴 EffectDisplay） ====================

        // 缓存反射获取的 ActiveEffects PropertyInfo
        private static PropertyInfo _activeEffectsProp;
        private static readonly object EffectsLock = new object();

        /// <summary>
        /// 通过反射获取玩家活跃效果并格式化为显示文本
        /// </summary>
        private string BuildEffectsDisplay(string userId)
        {
            try
            {
                if (!_plugin.Config.ShowActiveEffects) return "";

                var player = Player.List.FirstOrDefault(p => p != null && p.UserId == userId);
                if (player == null) return "";

                var activeEffects = GetActiveEffects(player);
                if (activeEffects == null) return "";

                var sb = new StringBuilder();
                int count = 0;

                foreach (object effect in activeEffects)
            {
                if (effect == null) continue;
                var t = effect.GetType();

                // IsEnabled
                var isEnabledProp = t.GetProperty("IsEnabled", BindingFlags.Public | BindingFlags.Instance);
                if (isEnabledProp == null) continue;
                if (!(bool)isEnabledProp.GetValue(effect)) continue;

                // TimeLeft
                float timeLeft = 0;
                var timeLeftProp = t.GetProperty("TimeLeft", BindingFlags.Public | BindingFlags.Instance);
                if (timeLeftProp != null)
                    timeLeft = (float)timeLeftProp.GetValue(effect);

                // Intensity
                int intensity = 0;
                var intensityProp = t.GetProperty("Intensity", BindingFlags.Public | BindingFlags.Instance);
                if (intensityProp != null)
                    intensity = Convert.ToInt32(intensityProp.GetValue(effect));

                // 跳过剩余时间很短的效果（闪烁）
                if (timeLeft < 1f && timeLeft > 0) continue;

                count++;
                string name = GetBuffDisplayName(t.Name);

                // 格式: "• 移速增强  强度1  |  剩余60秒"
                if (timeLeft > 0)
                {
                    int remaining = (int)Math.Ceiling(timeLeft);
                    sb.AppendLine($"<size=14><color=#00FF00>• {name}</color> <color=#AAAAAA>强度{intensity} | 剩余{remaining}秒</color></size>");
                }
                else
                {
                    sb.AppendLine($"<size=14><color=#00FF00>• {name}</color> <color=#AAAAAA>强度{intensity}</color></size>");
                }
            }

            if (count == 0) return "";
            return sb.ToString().TrimEnd('\r', '\n');
            }
            catch (Exception ex)
            {
                if (_plugin.Config.Debug)
                    Log.Debug($"BuildEffectsDisplay错误: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 通过反射获取玩家的 ActiveEffects
        /// </summary>
        private static IEnumerable GetActiveEffects(Player player)
        {
            if (_activeEffectsProp == null)
            {
                lock (EffectsLock)
                {
                    if (_activeEffectsProp == null)
                    {
                        _activeEffectsProp = typeof(Player).GetProperty("ActiveEffects",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }
            return _activeEffectsProp?.GetValue(player) as IEnumerable;
        }

        private static string GetBuffDisplayName(string englishName)
        {
            return englishName switch
            {
                "MovementBoost" => "移速增强",
                "Scp207" => "SCP-207",
                "Scp500" => "SCP-500",
                "Scp1344" => "SCP-1344",
                "Scp1853" => "SCP-1853",
                "Scp268" => "SCP-268",
                "Scp513" => "SCP-513",
                "AmnesiaItems" => "记忆丧失",
                "Asphyxiating" => "窒息",
                "Bleeding" => "流血",
                "Burned" => "烧伤",
                "Concussed" => "震荡",
                "Corroding" => "腐蚀",
                "Deafened" => "失聪",
                "Decontaminating" => "净化",
                "Disabled" => "瘫痪",
                "Ensnared" => "困缚",
                "Exhausted" => "疲劳",
                "Flashed" => "致盲",
                "Hemorrhage" => "大出血",
                "Hypothermia" => "低温",
                "Invigorated" => "振奋",
                "Poisoned" => "中毒",
                "SinkHole" => "陷阱",
                "Soundless" => "沉默",
                "Vitality" => "活力",
                "DamageReduction" => "减伤",
                "CardiacArrest" => "心脏骤停",
                "BodilyInjury" => "肢体损伤",
                _ => englishName
            };
        }

        // ==================== 护盾再生（每1秒） ====================

        public void RegenerateShields()
        {
            try
            {
                if (!_plugin.Config.EnableCommanderShield) return;
                var cfg = _plugin.Config;

                foreach (var kvp in _commanderShields.ToList())
                {
                    ShieldData sd = kvp.Value;
                    bool changed = false;

                    // AHP 再生 + 锁定（防止游戏自动衰减）
                    if (sd.AHP < sd.MaxAHP)
                    {
                        sd.AHP = Math.Min(sd.AHP + cfg.ShieldRegenPerTick, sd.MaxAHP);
                        changed = true;
                    }
                    // HS 再生
                    if (sd.HS < sd.MaxHS)
                    {
                        sd.HS = Math.Min(sd.HS + cfg.ShieldRegenPerTick, sd.MaxHS);
                        changed = true;
                    }

                    // 每次都将 AHP 写入游戏内置护盾（右下角蓝条显示 + 锁定防衰减）
                    var player = Player.List.FirstOrDefault(p => p != null && p.UserId == kvp.Key);
                    if (player != null)
                    {
                        // 将 AHP 写入游戏内置人工生命值 → 右下角血量旁边显示蓝条
                        player.ArtificialHealth = sd.AHP;
                        player.MaxArtificialHealth = sd.MaxAHP;
                    }
                }
            }
            catch (Exception ex) { Log.Error($"护盾再生: {ex.Message}"); }
        }

        // ==================== 指挥官护盾状态文字 ====================

        private string BuildShieldHud(string userId)
        {
            if (!_commanderShields.TryGetValue(userId, out ShieldData sd)) return "";
            if (sd.AHP <= 0 && sd.HS <= 0) return "";

            // 简短的盾量文字（蓝条已在右下角血量区自动显示）
            string text = _plugin.Config.ShieldHudText
                .Replace("{ahp}", sd.AHP.ToString())
                .Replace("{maxahp}", sd.MaxAHP.ToString())
                .Replace("{hs}", sd.HS.ToString())
                .Replace("{maxhs}", sd.MaxHS.ToString());
            string padding = new string('\n', 4);
            return $"{padding}<size=14><color=#00BFFF>{text}</color></size>";
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

        private string FormatStatusLine(PlayerData data, string userId)
        {
            string msg = _plugin.Config.StatusMessage;
            msg = msg.Replace("{player}", data.PlayerName);
            msg = msg.Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
            msg = msg.Replace("{exp}", data.Experience.ToString());
            msg = msg.Replace("{maxexp}", data.GetExpForNextLevel(_plugin.Config.BaseExpPerLevel).ToString());
            msg = msg.Replace("{time}", data.GetPlayTimeString());
            msg = msg.Replace("{kda}", GetRoundKDAString(userId));
            return msg;
        }

        // ==================== 回合控制 ====================

        public void OnRoundStarted()
        {
            _roundStartTime = DateTime.Now;
            CombatDataCache.Clear();
            _roundKills.Clear();
            _roundDeaths.Clear();
            _roundAssists.Clear();
            _assistDamage.Clear();
            _commanderShields.Clear();
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
