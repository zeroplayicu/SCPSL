using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Events.Handlers;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using PlayerRoles;

namespace ExperiencePlugin
{
    public class ExperienceEventHandler : CustomEventsHandler
    {
        private readonly ExperiencePlugin _plugin;
        public readonly Dictionary<string, CombatData> CombatDataCache = new Dictionary<string, CombatData>();
        private DateTime _roundStartTime;

        // ===== 本局战绩 KDA =====
        private readonly Dictionary<string, int> _roundKills = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundDeaths = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundAssists = new Dictionary<string, int>();

        // ===== 助攻伤害追踪: key=受害者UserId, value={攻击者UserId -> 总伤害}
        private readonly Dictionary<string, Dictionary<string, int>> _assistDamage = new Dictionary<string, Dictionary<string, int>>();

        public ExperienceEventHandler(ExperiencePlugin plugin) { _plugin = plugin; }

        public void RegisterRoundEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
            LabApi.Events.Handlers.ServerEvents.RoundEnded += OnRoundEnded;
        }

        public void UnregisterRoundEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
            LabApi.Events.Handlers.ServerEvents.RoundEnded -= OnRoundEnded;
        }

        // ==================== 玩家加入 ====================

        public override void OnPlayerJoined(PlayerJoinedEventArgs ev)
        {
            try { _plugin.DataManager.GetOrCreatePlayerData(ev.Player); }
            catch (Exception ex) { Logger.Error($"加入: {ex.Message}"); }
        }

        // ==================== 角色生成 - 发放等级buff ====================

        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                int level = data.Level;

                if (player.IsSCP)
                    ApplyScpBuff(player, level);
                else
                    ApplyHumanBuff(player, level);
            }
            catch (Exception ex) { Logger.Error($"生成buff: {ex.Message}"); }
        }

        private void ApplyScpBuff(Player player, int level)
        {
            int boost = 0;
            if (level >= 100) boost = 3;
            else if (level >= 50) boost = 2;
            else if (level >= 25) boost = 1;

            if (boost > 0)
            {
                ApplyScp207Effect(player, boost);
                Logger.Info($"[Buff] {player.Nickname} 等级{level} → SCP207×{boost}");
            }
        }

        private static void ApplyScp207Effect(Player player, int boost)
        {
            try
            {
                var method = typeof(Player).GetMethod("EnableEffect", new[] { typeof(byte), typeof(float), typeof(bool) });
                if (method == null) return;
                var effectType = Type.GetType("CustomPlayerEffects.Scp207, Assembly-CSharp");
                if (effectType == null) return;
                var genericMethod = method.MakeGenericMethod(effectType);
                genericMethod.Invoke(player, new object[] { (byte)boost, 9999f, false });
            }
            catch { }
        }

        private void ApplyHumanBuff(Player player, int level)
        {
            var role = player.Role;
            int boost = 0;

            if (role == RoleTypeId.ClassD)
            {
                if (level >= 0)
                {
                    if (Enum.TryParse<ItemType>(_plugin.Config.ClassDItem, out var itemType))
                        player.AddItem(itemType);
                }
                if (level >= 100) boost = 3;
                else if (level >= 50) boost = 2;
                else if (level >= 25) boost = 1;
            }
            else if (role == RoleTypeId.Scientist)
            {
                if (level >= 100) boost = 3;
                else if (level >= 50) boost = 2;
                else if (level >= 25) boost = 1;
            }
            else if (role == RoleTypeId.FacilityGuard ||
                     role == RoleTypeId.NtfPrivate ||
                     role == RoleTypeId.NtfSergeant ||
                     role == RoleTypeId.NtfSpecialist ||
                     role == RoleTypeId.NtfCaptain)
            {
                if (level >= 100) boost = 3;
                else if (level >= 50) boost = 2;
                else if (level >= 25) boost = 1;
            }

            if (boost > 0)
            {
                ApplyScp207Effect(player, boost);
                Logger.Info($"[Buff] {player.Nickname} 等级{level} → SCP207×{boost}");
            }
        }

        // ==================== 死亡不掉弹药 + 清空枪膛 ====================

        public override void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            try
            {
                // 清空备弹
                foreach (var ammoType in ev.Player.Ammo.Keys.ToList())
                    ev.Player.SetAmmo(ammoType, 0);
                // 清空当前武器的枪膛子弹
                UnloadAllFirearms(ev.Player);
            }
            catch (Exception ex) { Logger.Error($"Dying: {ex.Message}"); }
        }

        /// <summary>
        /// 通过反射清空玩家所有枪支的枪膛子弹，防止掉落迸射子弹
        /// </summary>
        private static void UnloadAllFirearms(Player player)
        {
            try
            {
                foreach (var item in player.Items)
                {
                    // 只处理枪支
                    string itemName = item.Type.ToString();
                    if (!itemName.Contains("Gun") && !itemName.Contains("Micro") && !itemName.Contains("Disruptor"))
                        continue;

                    // 获取该物品的 Base 对象（ItemBase）
                    var baseProp = item.GetType().GetProperty("Base");
                    if (baseProp == null) continue;
                    var itemBase = baseProp.GetValue(item);
                    if (itemBase == null) continue;

                    // 尝试获取 Status 属性中的 Ammo（枪支已上膛子弹数）
                    var statusProp = itemBase.GetType().GetProperty("Status");
                    if (statusProp == null) continue;
                    var status = statusProp.GetValue(itemBase);
                    if (status == null) continue;

                    // FirearmStatus 结构体有 Ammo 字段
                    var ammoField = status.GetType().GetField("Ammo");
                    if (ammoField == null) continue;
                    ammoField.SetValue(status, (byte)0);
                    statusProp.SetValue(itemBase, status);
                }
            }
            catch { }
        }

        // ==================== 清理子弹掉落物 ====================

        /// <summary>
        /// 扫描并销毁所有子弹类型的掉落物（防止死亡迸射满地子弹）
        /// </summary>
        private static void DestroyAmmoPickups()
        {
            try
            {
                var pickupType = Type.GetType("LabApi.Features.Wrappers.Pickups.Pickup, LabApi");
                if (pickupType == null) return;

                var listProp = pickupType.GetProperty("List");
                if (listProp == null) return;

                var list = listProp.GetValue(null) as System.Collections.IEnumerable;
                if (list == null) return;

                var typeProp = pickupType.GetProperty("Type");
                var destroyMethod = pickupType.GetMethod("Destroy");
                if (typeProp == null || destroyMethod == null) return;

                int removed = 0;
                foreach (var pickup in list)
                {
                    if (pickup == null) continue;
                    var itemType = (ItemType)typeProp.GetValue(pickup);

                    // 只清理子弹类
                    string tName = itemType.ToString();
                    if (tName.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        destroyMethod.Invoke(pickup, null);
                        removed++;
                    }
                }
                if (removed > 0)
                    Logger.Debug($"[清理] 销毁 {removed} 个子弹掉落物");
            }
            catch (Exception ex) { Logger.Error($"清理子弹错误: {ex.Message}"); }
        }

        // ==================== 伤害处理（经验累积 + 助攻追踪） ====================

        public override void OnPlayerHurt(PlayerHurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;

                // 通过反射获取伤害值（公共程序集可能属性名不同）
                float amount = 0f;
                var dmgProp = ev.DamageHandler.GetType().GetProperty("Damage");
                if (dmgProp != null)
                    amount = (float)dmgProp.GetValue(ev.DamageHandler);
                if (amount <= 0) return;

                int damage = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
                if (damage <= 0) damage = 1;

                string attackerId = ev.Attacker.UserId;
                string victimId = ev.Player.UserId;

                // === 1. 伤害经验累积 ===
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

                // === 3. 组伤检测（显示在攻击经验上） ===
                if (ev.Attacker.Faction == ev.Player.Faction && !ev.Player.IsSCP)
                {
                    if (!CombatDataCache.TryGetValue(attackerId, out CombatData cdPenalty))
                    {
                        cdPenalty = new CombatData();
                        CombatDataCache[attackerId] = cdPenalty;
                    }
                    cdPenalty.DisplayPenaltyXp += 1;
                    cdPenalty.LastFeedTime = DateTime.Now;
                }

                if (_plugin.Config.Debug)
                    Logger.Debug($"[伤害] {ev.Attacker.Nickname}: {damage} (累积XP: {cd.DisplayDamageXp})");
            }
            catch (Exception ex) { Logger.Error($"伤害事件: {ex.Message}"); }
        }

        // ==================== SCP207 无伤（多层检测） ====================

        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;

                // SCP207 无伤害 - 通过多种方式检测
                bool isScp207 = false;
                string typeName = ev.DamageHandler.GetType().Name;
                string fullName = ev.DamageHandler.GetType().FullName;

                // 方法1: 类型名包含 Scp207
                if (typeName.IndexOf("Scp207", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fullName.IndexOf("Scp207", StringComparison.OrdinalIgnoreCase) >= 0)
                    isScp207 = true;

                // 方法2: 类型名包含 207
                if (!isScp207 && typeName.IndexOf("207", StringComparison.OrdinalIgnoreCase) >= 0)
                    isScp207 = true;

                // 方法3: 检查 DamageHandler 是否是 Scp207DamageHandler 的实例
                if (!isScp207)
                {
                    try
                    {
                        var scp207Type = Type.GetType("CustomPlayerEffects.Scp207, Assembly-CSharp");
                        if (scp207Type != null)
                        {
                            // 检查玩家是否有活跃的Scp207效果
                            var refHub = ev.Player.GetType().GetProperty("ReferenceHub")?.GetValue(ev.Player);
                            var fxCtrl = refHub?.GetType().GetProperty("playerEffectsController")?.GetValue(refHub);
                            var allFx = fxCtrl?.GetType().GetProperty("AllEffects")?.GetValue(fxCtrl) as System.Collections.IEnumerable;
                            if (allFx != null)
                            {
                                foreach (var fx in allFx)
                                {
                                    if (fx != null && fx.GetType().Name.IndexOf("Scp207", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        var isEnabled = fx.GetType().GetProperty("IsEnabled")?.GetValue(fx);
                                        if (isEnabled is bool enabled && enabled)
                                        {
                                            isScp207 = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (_plugin.Config.Scp207NoDrain && isScp207)
                {
                    ev.IsAllowed = false;
                    if (_plugin.Config.Debug)
                        Logger.Debug($"[SCP207] 检测到类型:{typeName} → 已拦截");
                }
            }
            catch (Exception ex) { Logger.Error($"Hurting处理: {ex.Message}"); }
        }

        // ==================== 无限备弹（自动适配所有枪械） ====================

        /// <summary>
        /// 根据武器名称推断使用的弹药类型（字符串匹配，适配任何版本）
        /// </summary>
        private static ItemType GetAmmoTypeForWeapon(string weaponName)
        {
            // 5.56mm: MTF步枪/冲锋枪/机枪
            if (weaponName.IndexOf("E11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FRMG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FSP", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo556x45;

            // 7.62mm: 混沌步枪/机枪
            if (weaponName.IndexOf("AK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Logicer", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo762x39;

            // 9mm: 手枪/冲锋枪/左轮
            if (weaponName.IndexOf("COM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Crossvec", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Revolver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FSP", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo9x19;

            // 12号口径: 霰弹枪
            if (weaponName.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo9x19; // 老旧DLL无Ammo12Gauge，用9mm代替

            return ItemType.None;
        }

        /// <summary>
        /// 获取枪械最大弹容量（通过反射尝试多种方式）
        /// </summary>
        private static int GetFirearmMaxAmmo(object firearmItem)
        {
            try
            {
                var baseProp = firearmItem.GetType().GetProperty("Base");
                if (baseProp == null) return 30;
                var firearmBase = baseProp.GetValue(firearmItem);
                if (firearmBase == null) return 30;

                // 1. 尝试 GetMaxAmmo() 方法
                var getMaxMethod = firearmBase.GetType().GetMethod("GetMaxAmmo", Type.EmptyTypes);
                if (getMaxMethod != null)
                    return (int)getMaxMethod.Invoke(firearmBase, null);

                // 2. 尝试 Status.MaxAmmo 属性
                var statusProp = firearmBase.GetType().GetProperty("Status");
                if (statusProp != null)
                {
                    var status = statusProp.GetValue(firearmBase);
                    if (status != null)
                    {
                        var maxAmmoField = status.GetType().GetField("MaxAmmo");
                        if (maxAmmoField != null)
                            return (int)maxAmmoField.GetValue(status);
                    }
                }

                // 3. 尝试 MaxAmmo 直接属性
                var maxAmmoProp = firearmBase.GetType().GetProperty("MaxAmmo");
                if (maxAmmoProp != null)
                    return (int)maxAmmoProp.GetValue(firearmBase);
            }
            catch { }
            return 30;
        }

        public override void OnPlayerReloadingWeapon(PlayerReloadingWeaponEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;

                var firearmItem = ev.GetType().GetProperty("FirearmItem")?.GetValue(ev);
                if (firearmItem == null) return;

                ItemType ammoType = ItemType.None;

                // 方法1：直接从FirearmItem获取AmmoType属性
                var ammoProp = firearmItem.GetType().GetProperty("AmmoType");
                if (ammoProp != null)
                    ammoType = (ItemType)ammoProp.GetValue(firearmItem);

                // 方法2：通过武器类型名称推断
                if (ammoType == ItemType.None)
                {
                    var typeProp = firearmItem.GetType().GetProperty("Type");
                    if (typeProp != null)
                    {
                        var weaponType = (ItemType)typeProp.GetValue(firearmItem);
                        ammoType = GetAmmoTypeForWeapon(weaponType.ToString());
                    }
                }

                // 方法3：遍历玩家弹药类型，全都设为最大+1（保底方案）
                if (ammoType == ItemType.None)
                {
                    foreach (var at in ev.Player.Ammo.Keys.ToList())
                        ev.Player.SetAmmo(at, 999);
                    return;
                }

                // 获取最大弹容量，备弹设为最大+1
                int maxAmmo = GetFirearmMaxAmmo(firearmItem);
                ev.Player.SetAmmo(ammoType, (ushort)(maxAmmo + 1));

                if (_plugin.Config.Debug)
                    Logger.Debug($"[备弹] {ev.Player.Nickname} {ammoType} → {maxAmmo + 1}");
            }
            catch (Exception ex) { Logger.Error($"换弹: {ex.Message}"); }
        }

        // ==================== 丢枪清零弹药 + 清空枪膛（防止迸射子弹） ====================

        public override void OnPlayerDroppingItem(PlayerDroppingItemEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;

                // 只处理枪支类物品
                string itemName = ev.Item.Type.ToString();
                if (!itemName.Contains("Gun") && !itemName.Contains("Micro") && !itemName.Contains("Disruptor"))
                    return;

                // 清空目标枪支的枪膛
                UnloadSpecificFirearm(ev.Player, ev.Item);
                // 清空备弹
                foreach (var ammoType in ev.Player.Ammo.Keys.ToList())
                    ev.Player.SetAmmo(ammoType, 0);
            }
            catch (Exception ex) { Logger.Error($"丢枪: {ex.Message}"); }
        }

        /// <summary>
        /// 清空指定物品（枪支）的枪膛子弹
        /// </summary>
        private static void UnloadSpecificFirearm(Player player, Item item)
        {
            try
            {
                var baseProp = item.GetType().GetProperty("Base");
                if (baseProp == null) return;
                var itemBase = baseProp.GetValue(item);
                if (itemBase == null) return;

                var statusProp = itemBase.GetType().GetProperty("Status");
                if (statusProp == null) return;
                var status = statusProp.GetValue(itemBase);
                if (status == null) return;

                var ammoField = status.GetType().GetField("Ammo");
                if (ammoField == null) return;
                ammoField.SetValue(status, (byte)0);
                statusProp.SetValue(itemBase, status);
            }
            catch { }
        }

        // ==================== 击杀事件 + 助攻结算 ====================

        public override void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            try
            {
                string victimId = ev.Player.UserId;
                _plugin.DataManager.AddDeath(victimId);
                AddRoundDeath(victimId);

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
                        ev.Attacker.SendHint(GetFullHint(killerId) + lvlMsg, (ushort)(_plugin.Config.StatusRefreshInterval + 2));
                    }
                }

                // --- 助攻结算 ---
                ProcessAssists(ev);

                // --- 组杀反馈 ---
                if (ev.Attacker != null && ev.Attacker != ev.Player &&
                    ev.Attacker.Faction == ev.Player.Faction && !ev.Player.IsSCP)
                {
                    string xpMsg = $"\n\n\n\n\n\n\n\n\n\n\n<size=26><color=#FF4444>击杀队友 -200xp</color></size>";
                    ev.Attacker.SendHint(xpMsg, 4);
                }

                // --- 清理玩家死亡掉落的子弹拾取物 ---
                DestroyAmmoPickups();
            }
            catch (Exception ex) { Logger.Error($"死亡: {ex.Message}"); }
        }

        private void ProcessAssists(PlayerDeathEventArgs ev)
        {
            string victimId = ev.Player.UserId;

            if (!_assistDamage.TryGetValue(victimId, out var attackerDict)) return;

            bool victimIsScp = ev.Player.IsSCP;
            string killerId = (ev.Attacker != null && ev.Attacker != ev.Player) ? ev.Attacker.UserId : null;

            foreach (var kvp in attackerDict)
            {
                string attackerId = kvp.Key;
                int damageDealt = kvp.Value;

                if (attackerId == killerId) continue;

                bool giveAssist = false;
                int assistExp = 0;

                if (victimIsScp)
                {
                    if (damageDealt >= _plugin.Config.ScpAssistThreshold)
                        giveAssist = true;
                }
                else
                {
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
                                Logger.Debug($"[助攻] {assister.Nickname}: 助攻{damageDealt}伤害 → +{assistExp}xp" +
                                    (leveledUp ? " (升级!)" : ""));
                        }
                    }
                }
            }

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
            catch (Exception ex) { Logger.Error($"结算检查: {ex.Message}"); }
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
                player.SendHint(hint, 5);
            }
            catch (Exception ex) { Logger.Error($"结算: {ex.Message}"); }
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
            catch (Exception ex) { Logger.Error($"批量刷新: {ex.Message}"); }
        }

        private void RefreshPlayerPanel(Player player)
        {
            try
            {
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;
                player.SendHint(GetFullHint(player.UserId), (ushort)(_plugin.Config.StatusRefreshInterval + 2));
            }
            catch (Exception ex) { Logger.Error($"刷新面板: {ex.Message}"); }
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

            // 效果常驻显示
            string effects = BuildEffectsDisplay(userId);
            if (!string.IsNullOrEmpty(effects))
                parts.Add(effects);

            // 底部状态栏（经验 + KDA）
            string status = FormatStatusLine(data, userId);
            parts.Add(status);

            return string.Join("\n\n\n\n", parts);
        }

        // ==================== 效果常驻显示 ====================

        private string BuildEffectsDisplay(string userId)
        {
            try
            {
                if (!_plugin.Config.ShowActiveEffects) return "";

                var player = Player.List.FirstOrDefault(p => p != null && p.UserId == userId);
                if (player == null) return "";

                // 通过全反射获取效果列表，避免编译时依赖游戏类型
                var playerType = player.GetType();
                var referenceHub = playerType.GetProperty("ReferenceHub", BindingFlags.Public | BindingFlags.Instance)?.GetValue(player);
                if (referenceHub == null) return "";
                var effectsController = referenceHub.GetType().GetProperty("playerEffectsController")?.GetValue(referenceHub);
                var allEffectsObj = effectsController?.GetType().GetProperty("AllEffects")?.GetValue(effectsController);
                if (allEffectsObj == null) return "";

                var sb = new StringBuilder();
                int count = 0;

                foreach (var effectObj in (System.Collections.IEnumerable)allEffectsObj)
                {
                    if (effectObj == null) continue;
                    var et = effectObj.GetType();

                    var isEnabledProp = et.GetProperty("IsEnabled");
                    if (isEnabledProp == null) continue;
                    if (!(bool)isEnabledProp.GetValue(effectObj)) continue;

                    float timeLeft = 0f;
                    var timeLeftProp = et.GetProperty("TimeLeft");
                    if (timeLeftProp != null)
                        timeLeft = (float)timeLeftProp.GetValue(effectObj);

                    int intensity = 0;
                    var intensityProp = et.GetProperty("Intensity");
                    if (intensityProp != null)
                        intensity = Convert.ToInt32(intensityProp.GetValue(effectObj));

                    if (timeLeft < 1f && timeLeft > 0) continue;

                    count++;
                    string name = GetBuffDisplayName(effectObj.GetType().Name);

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
                    Logger.Debug($"BuildEffectsDisplay错误: {ex.Message}");
                return "";
            }
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
            if (cd.DisplayPenaltyXp > 0)
                lines.Add($"<size=22><color=#FF4444>攻击队友 -{cd.DisplayPenaltyXp}xp</color></size>");
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

        // ==================== 回合控制（直接订阅） ====================

        private void OnRoundStarted()
        {
            _roundStartTime = DateTime.Now;
            CombatDataCache.Clear();
            _roundKills.Clear();
            _roundDeaths.Clear();
            _roundAssists.Clear();
            _assistDamage.Clear();
            Logger.Info("回合开始");
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
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
            catch (Exception ex) { Logger.Error($"回合结束: {ex.Message}"); }
        }
    }
}
