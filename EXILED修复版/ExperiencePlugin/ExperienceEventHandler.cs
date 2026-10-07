using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using InventorySystem.Items;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ExperiencePlugin
{
    public class ExperienceEventHandler
    {
        private readonly ExperiencePlugin _plugin;
        public readonly Dictionary<string, CombatData> CombatDataCache = new Dictionary<string, CombatData>();
        private DateTime _roundStartTime;

        // ===== 本局战绩 KDA =====
        private readonly Dictionary<string, int> _roundKills = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundDeaths = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _roundAssists = new Dictionary<string, int>();

        // ===== 助攻伤害追踪 =====
        private readonly Dictionary<string, Dictionary<string, int>> _assistDamage = new Dictionary<string, Dictionary<string, int>>();

        // ===== 击杀播报缓冲区 =====
        private readonly List<(string Text, DateTime Time)> _killFeed = new List<(string, DateTime)>();
        private const int MaxKillFeed = 5;
        private const int KillFeedLifetime = 15;

        public ExperienceEventHandler(ExperiencePlugin plugin) { _plugin = plugin; }

        // ==================== 玩家加入 ====================

        public void OnVerified(VerifiedEventArgs ev)
        {
            try
            {
                var data = _plugin.DataManager.GetOrCreatePlayerData(ev.Player);
                SetVipBadge(ev.Player, data);
            }
            catch (Exception ex) { Log.Error($"加入: {ex.Message}"); }
        }

        private static void SetVipBadge(Player player, PlayerData data)
        {
            try
            {
                if (player == null || data == null) return;
                if (data.VipLevel >= 2)
                    player.CustomName = $"<color=#FF69B4>【SVIP】</color> {player.Nickname}";
                else if (data.VipLevel >= 1)
                    player.CustomName = $"<color=#FFD700>【VIP】</color> {player.Nickname}";
            }
            catch { }
        }

        // ==================== 角色生成 - 发放等级buff ====================

        public void OnSpawned(SpawnedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                // 刷新 VIP 头衔（因为死亡会重置 CustomName）
                SetVipBadge(player, data);

                int level = data.Level;

                if (player.IsScp)
                    ApplyScpBuff(player, level, data);
                else
                    ApplyHumanBuff(player, level, data);

                // AntiSCP207 仅在启用无伤时单独添加（等级增益不附带）
                if (_plugin.Config.Scp207NoDrain)
                    player.EnableEffect(EffectType.AntiScp207, 9999f);
            }
            catch (Exception ex) { Log.Error($"生成buff: {ex.Message}"); }
        }

        /// <summary>给人类发放 SCP207 物品（不含AntiSCP207）</summary>
        private static void GiveScp207(Player player, int boost)
        {
            try
            {
                for (int i = 0; i < boost; i++)
                    player.AddItem(ItemType.SCP207);
            }
            catch { }
        }

        private void ApplyScpBuff(Player player, int level, PlayerData data)
        {
            int boost = 0;
            if (level >= 100) boost = 3;
            else if (level >= 50) boost = 2;
            else if (level >= 25) boost = 1;

            if (boost > 0)
            {
                ApplyScp207EffectDirect(player, boost);
                Log.Info($"[Buff-SCP] {player.Nickname} 等级{level} VIP{data.VipLevel} → SCP207×{boost}");
            }
        }

        private static void ApplyScp207EffectDirect(Player player, int boost)
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

        private void ApplyHumanBuff(Player player, int level, PlayerData data)
        {
            var role = player.Role.Type;
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

            // VIP/SVIP 不增加 SCP207 数量，只享受经验/积分倍率

            if (boost > 0)
            {
                GiveScp207(player, boost);
                Log.Info($"[Buff] {player.Nickname} 等级{level} VIP{data.VipLevel} → SCP207×{boost}");
            }
        }

        // ==================== 死亡不掉弹药 + 清空枪膛 ====================

        public void OnDying(DyingEventArgs ev)
        {
            try
            {
                // 清空备弹（访问游戏原生Inventory）
                foreach (var ammoType in ev.Player.Ammo.Keys.ToList())
                {
                    try
                    {
                        ev.Player.ReferenceHub.inventory.UserInventory.ReserveAmmo[ammoType] = 0;
                    }
                    catch { }
                }
                UnloadAllFirearms(ev.Player);
            }
            catch (Exception ex) { Log.Error($"Dying: {ex.Message}"); }
        }

        private static void UnloadAllFirearms(Player player)
        {
            try
            {
                foreach (var item in player.Items)
                {
                    string itemName = item.Type.ToString();
                    if (!itemName.Contains("Gun") && !itemName.Contains("Micro") && !itemName.Contains("Disruptor"))
                        continue;

                    var baseProp = item.GetType().GetProperty("Base");
                    if (baseProp == null) continue;
                    var itemBase = baseProp.GetValue(item);
                    if (itemBase == null) continue;

                    var statusProp = itemBase.GetType().GetProperty("Status");
                    if (statusProp == null) continue;
                    var status = statusProp.GetValue(itemBase);
                    if (status == null) continue;

                    var ammoField = status.GetType().GetField("Ammo");
                    if (ammoField == null) continue;
                    ammoField.SetValue(status, (byte)0);
                    statusProp.SetValue(itemBase, status);
                }
            }
            catch { }
        }

        // ==================== 清理子弹掉落物 ====================

        private static void DestroyAmmoPickups()
        {
            try
            {
                foreach (var pickup in Exiled.API.Features.Pickups.Pickup.List.ToList())
                {
                    if (pickup == null || !pickup.IsSpawned) continue;
                    string tName = pickup.Type.ToString();
                    if (tName.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) >= 0)
                        pickup.Destroy();
                }
            }
            catch (Exception ex) { Log.Error($"清理子弹错误: {ex.Message}"); }
        }

        // ==================== 击杀信息辅助方法 ====================

        private static string GetWeaponDisplayName(object damageHandler)
        {
            try
            {
                string typeName = damageHandler.GetType().Name;
                if (typeName.IndexOf("Firearm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var weaponProp = damageHandler.GetType().GetProperty("WeaponType");
                    if (weaponProp != null)
                    {
                        var weaponType = weaponProp.GetValue(damageHandler);
                        if (weaponType != null)
                            return GetShortWeaponName(weaponType.ToString());
                    }
                }
                if (typeName.IndexOf("Revolver", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "左轮";
                if (typeName.IndexOf("MicroHid", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "MicroHID";
                if (typeName.IndexOf("Scp018", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "SCP-018";
                if (typeName.IndexOf("Scp207", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "SCP-207";
                if (typeName.IndexOf("FriendlyFire", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "友伤";
                if (typeName.IndexOf("Custom", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "未知";
                if (typeName.IndexOf("Explosion", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "爆炸";
                if (typeName.IndexOf("Tesla", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "特斯拉";
                if (typeName.IndexOf("Recontain", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "收容";
                return "击杀";
            }
            catch { return ""; }
        }

        private static string GetShortWeaponName(string weaponType)
        {
            return weaponType switch
            {
                "GunE11SR" => "E-11 SR", "GunFRMG0" => "FR-MG-0", "GunAK" => "AK",
                "GunLogicer" => "Logicer", "GunCOM15" => "COM15", "GunCOM18" => "COM18",
                "GunCrossvec" => "Crossvec", "GunRevolver" => "左轮", "GunShotgun" => "霰弹",
                "GunFSP9" => "FSP9", "GunSCP127" => "SCP-127", "MicroHID" => "MicroHID",
                _ => weaponType.Replace("Gun", "")
            };
        }

        private static string GetSpecialKillTag(object damageHandler, Player attacker)
        {
            try
            {
                string typeName = damageHandler.GetType().Name;
                if (typeName.IndexOf("Headshot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "<color=#FFD700>🎯</color> ";

                try
                {
                    var isHeadshot = damageHandler.GetType().GetProperty("IsHeadshot");
                    if (isHeadshot != null && (bool)isHeadshot.GetValue(damageHandler))
                        return "<color=#FFD700>🎯</color> ";
                }
                catch { }

                return "";
            }
            catch { return ""; }
        }

        // ==================== 伤害处理 ====================

        public void OnHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;

                float amount = ev.Amount;
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

                // === 3. 组伤检测 ===
                if (ev.Attacker.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp)
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
                    Log.Debug($"[伤害] {ev.Attacker.Nickname}: {damage} (累积XP: {cd.DisplayDamageXp})");
            }
            catch (Exception ex) { Log.Error($"伤害事件: {ex.Message}"); }
        }

        // ==================== SCP207 无伤 (通过AntiSCP207事件) ====================

        public void OnSavingByAntiScp207(SavingByAntiScp207EventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;

                if (_plugin.Config.Scp207NoDrain)
                {
                    ev.IsAllowed = true;
                    ev.DamageMultiplier = 0f;
                }
            }
            catch (Exception ex) { Log.Error($"SavingByAntiScp207: {ex.Message}"); }
        }

        // ==================== 无限备弹 ====================

        private static ItemType GetAmmoTypeForWeapon(string weaponName)
        {
            if (weaponName.IndexOf("E11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FRMG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FSP", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo556x45;

            if (weaponName.IndexOf("AK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Logicer", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo762x39;

            if (weaponName.IndexOf("COM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Crossvec", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Revolver", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo9x19;

            if (weaponName.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0)
                return ItemType.Ammo9x19;

            return ItemType.None;
        }

        private static int GetFirearmMaxAmmo(object firearmItem)
        {
            try
            {
                var baseProp = firearmItem.GetType().GetProperty("Base");
                if (baseProp == null) return 30;
                var firearmBase = baseProp.GetValue(firearmItem);
                if (firearmBase == null) return 30;

                var getMaxMethod = firearmBase.GetType().GetMethod("GetMaxAmmo", Type.EmptyTypes);
                if (getMaxMethod != null)
                    return (int)getMaxMethod.Invoke(firearmBase, null);

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

                var maxAmmoProp = firearmBase.GetType().GetProperty("MaxAmmo");
                if (maxAmmoProp != null)
                    return (int)maxAmmoProp.GetValue(firearmBase);
            }
            catch { }
            return 30;
        }

        public void OnReloadingWeapon(ReloadingWeaponEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;

                var firearm = ev.Firearm;
                if (firearm == null) return;

                ItemType ammoType = ItemType.None;

                // 方法1：从Firearm获取AmmoType
                try
                {
                    var ammoProp = firearm.GetType().GetProperty("AmmoType");
                    if (ammoProp != null)
                        ammoType = (ItemType)ammoProp.GetValue(firearm);
                }
                catch { }

                // 方法2：通过武器类型名称推断
                if (ammoType == ItemType.None)
                {
                    var typeProp = firearm.GetType().GetProperty("Type");
                    if (typeProp != null)
                    {
                        var weaponType = (ItemType)typeProp.GetValue(firearm);
                        ammoType = GetAmmoTypeForWeapon(weaponType.ToString());
                    }
                }

                // 方法3：保底方案 - 所有弹药设为999
                if (ammoType == ItemType.None)
                {
                    foreach (var at in ev.Player.Ammo.Keys.ToList())
                        ev.Player.ReferenceHub.inventory.UserInventory.ReserveAmmo[at] = 999;
                    return;
                }

                int maxAmmo = GetFirearmMaxAmmo(firearm);
                int currentMag = GetMagazineAmmo(firearm);
                int needed = maxAmmo - currentMag;
                if (needed <= 0) needed = 1;

                ev.Player.ReferenceHub.inventory.UserInventory.ReserveAmmo[ammoType] = (ushort)needed;

                if (_plugin.Config.Debug)
                    Log.Debug($"[备弹] {ev.Player.Nickname} {ammoType} 弹匣{currentMag}/{maxAmmo} → 补{needed}发");
            }
            catch (Exception ex) { Log.Error($"换弹: {ex.Message}"); }
        }

        private static int GetMagazineAmmo(object firearmItem)
        {
            try
            {
                var baseProp = firearmItem.GetType().GetProperty("Base");
                if (baseProp == null) return 0;
                var firearmBase = baseProp.GetValue(firearmItem);
                if (firearmBase == null) return 0;

                var statusProp = firearmBase.GetType().GetProperty("Status");
                if (statusProp != null)
                {
                    var status = statusProp.GetValue(firearmBase);
                    if (status != null)
                    {
                        var ammoField = status.GetType().GetField("Ammo");
                        if (ammoField != null)
                            return (int)(byte)ammoField.GetValue(status);
                    }
                }
            }
            catch { }
            return 0;
        }

        // ==================== 拾取枪械给1发子弹 ====================

        public void OnItemAdded(ItemAddedEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;
                if (ev.Item == null || ev.Pickup == null) return; // 仅处理拾取（非直接添加）

                var player = ev.Player;
                if (player == null) return;

                string typeName = ev.Item.Type.ToString();
                if (!typeName.Contains("Gun") && !typeName.Contains("Revolver") && !typeName.Contains("Crossvec"))
                    return;

                var ammoType = GetAmmoTypeForWeapon(typeName);
                if (ammoType == ItemType.None) return;

                ushort current = 0;
                if (player.Ammo.TryGetValue(ammoType, out var val))
                    current = val;

                if (current < 1)
                    player.ReferenceHub.inventory.UserInventory.ReserveAmmo[ammoType] = 1;

                if (_plugin.Config.Debug)
                    Log.Debug($"[拾取] {player.Nickname} 捡起{typeName} → 补1发{ammoType}");
            }
            catch (Exception ex) { Log.Error($"拾取: {ex.Message}"); }
        }

        // ==================== 丢枪清零弹药 ====================

        public void OnDroppingItem(DroppingItemEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;

                string itemName = ev.Item.Type.ToString();
                if (!itemName.Contains("Gun") && !itemName.Contains("Micro") && !itemName.Contains("Disruptor"))
                    return;

                UnloadSpecificFirearm(ev.Player, ev.Item);
                foreach (var ammoType in ev.Player.Ammo.Keys.ToList())
                    ev.Player.ReferenceHub.inventory.UserInventory.ReserveAmmo[ammoType] = 0;
            }
            catch (Exception ex) { Log.Error($"丢枪: {ex.Message}"); }
        }

        private static void UnloadSpecificFirearm(Player player, Exiled.API.Features.Items.Item item)
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

        public void OnDied(DiedEventArgs ev)
        {
            try
            {
                string victimId = ev.Player.UserId;
                _plugin.DataManager.AddDeath(victimId);
                AddRoundDeath(victimId);

                if (CombatDataCache.TryGetValue(victimId, out CombatData victimCd))
                    victimCd.KillStreak = 0;

                if (ev.Attacker != null && ev.Attacker != ev.Player)
                {
                    string killerId = ev.Attacker.UserId;
                    int killExp = _plugin.Config.ExpPerKill;
                    bool leveledUp = _plugin.DataManager.AddExperience(ev.Attacker, killExp);
                    _plugin.DataManager.AddKill(killerId);
                    float ptsGained = _plugin.DataManager.AddPoints(killerId, _plugin.Config.PointsPerKill);
                    ShowPointsNotif(ev.Attacker, $"+{ptsGained:F1} 积分 (击杀)");
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
                        ev.Attacker.ShowHint(lvlMsg, 4);
                    }
                }

                ProcessAssists(ev);

                if (ev.Attacker != null && ev.Attacker != ev.Player)
                {
                    try
                    {
                        string weaponName = GetWeaponDisplayName(ev.DamageHandler);
                        string specialTag = GetSpecialKillTag(ev.DamageHandler, ev.Attacker);
                        string killerColor = GetRoleColor(ev.Attacker);
                        string killMsg = $"<size=18><color={killerColor}>{ev.Attacker.Nickname}</color>" +
                            $"<color=white> {specialTag}🔫 </color>" +
                            $"<color=#FF4444>{ev.Player.Nickname}</color></size>";

                        // 堆叠到击杀播报缓冲区
                        _killFeed.Add((killMsg, DateTime.Now));
                        while (_killFeed.Count > MaxKillFeed)
                            _killFeed.RemoveAt(0);

                        // 刷新所有玩家的击杀播报
                        RefreshKillFeed();
                    }
                    catch { }
                }

                if (ev.Attacker != null && ev.Attacker != ev.Player &&
                    ev.Attacker.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp)
                {
                    string xpMsg = $"\n\n\n\n\n\n\n\n\n\n\n<size=26><color=#FF4444>击杀队友 -200xp</color></size>";
                    ev.Attacker.ShowHint(xpMsg, 4);
                }

                DestroyAmmoPickups();
            }
            catch (Exception ex) { Log.Error($"死亡: {ex.Message}"); }
        }

        private void ProcessAssists(DiedEventArgs ev)
        {
            string victimId = ev.Player.UserId;

            if (!_assistDamage.TryGetValue(victimId, out var attackerDict)) return;

            bool victimIsScp = ev.Player.IsScp;
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
                        float ptsGained = _plugin.DataManager.AddPoints(attackerId, _plugin.Config.PointsPerAssist);
                        ShowPointsNotif(assister, $"+{ptsGained:F1} 积分 (助攻)");

                        if (assistExp > 0)
                        {
                            bool leveledUp = _plugin.DataManager.AddExperience(assister, assistExp);
                            if (_plugin.Config.Debug)
                                Log.Debug($"[助攻] {assister.Nickname}: 助攻{damageDealt}伤害 → +{assistExp}xp" +
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
                string status = FormatStatusLine(data, userId);
                string hint = settle;
                if (leveledUp)
                    hint += "\n<size=26><color=lime>升级！{level}</color></size>"
                        .Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
                hint += "\n" + status;
                PlayerDisplay.Get(player).ShowHint(new HsmHint { Id = "settle_hint", Text = hint, FontSize = 12, YCoordinate = 930 }, 5);
            }
            catch (Exception ex) { Log.Error($"结算: {ex.Message}"); }
        }

        // ==================== 统一显示刷新 ====================

        /// <summary>timer 调用：每秒统一刷新所有玩家显示</summary>
        public void RefreshAllDisplays()
        {
            try
            {
                Commands.ChatMessageBuffer.TickCountdowns();
                CheckAndSettleDamage();

                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)))
                    RefreshDisplay(player);
            }
            catch (Exception ex) { Log.Error($"统一刷新: {ex.Message}"); }
        }

        /// <summary>单个玩家显示刷新 — 合并 BC + 击杀播报 + 战斗反馈 + 效果 + 状态栏</summary>
        public void RefreshDisplay(Player player)
        {
            try
            {
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                string hint = BuildCombinedHint(player.UserId);
                PlayerDisplay.Get(player).ShowHint(
                    new HsmHint
                    {
                        Id = "display",
                        Text = hint,
                        FontSize = 10,
                        YCoordinate = 1060,
                        Alignment = HintAlignment.Center
                    }, 3f);
            }
            catch (Exception ex) { Log.Error($"显示刷新: {ex.Message}"); }
        }

        /// <summary>构建合并显示文本</summary>
        private string BuildCombinedHint(string userId)
        {
            var sb = new StringBuilder();

            // ---- 顶部区域：BC聊天（小字，浅色） ----
            string bcText = Commands.ChatMessageBuffer.BuildBcOnly();
            if (!string.IsNullOrEmpty(bcText))
            {
                sb.Append("<size=11><color=#AAAAAA>");
                sb.Append(bcText);
                sb.Append("</color></size>");
                sb.Append('\n');
            }

            // ---- 击杀播报 ----
            var cutoff = DateTime.Now.AddSeconds(-KillFeedLifetime);
            _killFeed.RemoveAll(x => x.Time < cutoff);
            if (_killFeed.Count > 0)
            {
                var killLines = _killFeed.Select(x => x.Text);
                sb.Append(string.Join("\n", killLines));
                sb.Append('\n');
            }

            // ---- 战斗反馈 ----
            string combatFeed = BuildCombatFeed(userId);
            if (!string.IsNullOrEmpty(combatFeed))
            {
                sb.Append(combatFeed);
                sb.Append('\n');
            }

            // ---- 效果显示 ----
            string effects = BuildEffectsDisplay(userId);
            if (!string.IsNullOrEmpty(effects))
            {
                sb.Append(effects);
                sb.Append('\n');
            }

            // ---- 状态栏 ----
            var data = _plugin.DataManager.GetPlayerData(userId);
            string status = FormatStatusLine(data, userId);
            sb.Append(status);

            return sb.ToString();
        }

        // ---- 保留旧方法作为内部调用 ----

        private void RefreshPlayerPanel(Player player) => RefreshDisplay(player);

        private void RefreshKillFeed()
        {
            // 击杀后立即刷新所有玩家显示
            try
            {
                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)))
                    RefreshDisplay(player);
            }
            catch { }
        }

        // ==================== 效果常驻显示 ====================

        private string BuildEffectsDisplay(string userId)
        {
            try
            {
                if (!_plugin.Config.ShowActiveEffects) return "";

                var player = Player.List.FirstOrDefault(p => p != null && p.UserId == userId);
                if (player == null) return "";

                var sb = new StringBuilder();
                int count = 0;

                foreach (var effect in player.ActiveEffects)
                {
                    if (effect == null || !effect.IsEnabled) continue;

                    float timeLeft = effect.TimeLeft;
                    byte intensity = effect.Intensity;

                    if (timeLeft < 1f && timeLeft > 0) continue;

                    count++;
                    string name = GetBuffDisplayName(effect.GetType().Name);

                    if (timeLeft > 0)
                    {
                        int remaining = (int)Math.Ceiling(timeLeft);
                        sb.AppendLine($"<size=12><color=#00FF00>• {name}</color> <color=#AAAAAA>强度{intensity} | 剩余{remaining}秒</color></size>");
                    }
                    else
                    {
                        sb.AppendLine($"<size=12><color=#00FF00>• {name}</color> <color=#AAAAAA>强度{intensity}</color></size>");
                    }
                }

                if (count == 0) return "";
                return sb.ToString().TrimEnd('\r', '\n');
            }
            catch (Exception ex)
            {
                if (_plugin.Config.Debug) Log.Debug($"BuildEffectsDisplay错误: {ex.Message}");
                return "";
            }
        }

        private static string GetBuffDisplayName(string englishName)
        {
            return englishName switch
            {
                "MovementBoost" => "移速增强", "Scp207" => "SCP-207", "Scp500" => "SCP-500",
                "Scp1344" => "SCP-1344", "Scp1853" => "SCP-1853", "Scp268" => "SCP-268",
                "Scp513" => "SCP-513", "AmnesiaItems" => "记忆丧失", "Asphyxiating" => "窒息",
                "Bleeding" => "流血", "Burned" => "烧伤", "Concussed" => "震荡",
                "Corroding" => "腐蚀", "Deafened" => "失聪", "Decontaminating" => "净化",
                "Disabled" => "瘫痪", "Ensnared" => "困缚", "Exhausted" => "疲劳",
                "Flashed" => "致盲", "Hemorrhage" => "大出血", "Hypothermia" => "低温",
                "Invigorated" => "振奋", "Poisoned" => "中毒", "SinkHole" => "陷阱",
                "Soundless" => "沉默", "Vitality" => "活力", "DamageReduction" => "减伤",
                "CardiacArrest" => "心脏骤停", "BodilyInjury" => "肢体损伤",
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
                lines.Add($"<size=14><color=#FF4444>攻击队友 -{cd.DisplayPenaltyXp}xp</color></size>");
            return string.Join("\n", lines);
        }

        private string FormatStatusLine(PlayerData data, string userId)
        {
            string msg = _plugin.Config.StatusMessage;
            var player = Player.List.FirstOrDefault(p => p != null && p.UserId == userId);
            string color = GetRoleColor(player);

            // VIP/SVIP徽标
            string vipBadge = "";
            if (data.VipLevel >= 2) vipBadge = _plugin.Config.SvipBadge;
            else if (data.VipLevel >= 1) vipBadge = _plugin.Config.VipBadge;
            msg = msg.Replace("{vip}", vipBadge);

            msg = msg.Replace("{player}", data.PlayerName);
            msg = msg.Replace("{level}", data.Level.ToString());
            msg = msg.Replace("{exp}", data.Experience.ToString());
            msg = msg.Replace("{maxexp}", data.GetExpForNextLevel(_plugin.Config.BaseExpPerLevel).ToString());
            msg = msg.Replace("{time}", data.GetPlayTimeString());
            msg = msg.Replace("{kda}", GetRoundKDAString(userId));
            msg = msg.Replace("{points}", data.Points.ToString("F1"));
            return $"<size=12><color={color}>{msg}</color></size>";
        }

        private static string GetRoleColor(Player player)
        {
            if (player == null) return "#FFFFFF";
            return player.Role.Team switch
            {
                Team.SCPs => "#FF5555",
                Team.FoundationForces => _GetNtfColor(player),
                Team.ChaosInsurgency => "#55DD55",
                Team.Scientists => "#FFDD44",
                Team.ClassD => "#FF9922",
                _ => _GetMiscColor(player)
            };
        }

        private static string _GetNtfColor(Player player)
        {
            return player.Role.Type == RoleTypeId.FacilityGuard ? "#88AACC" : "#5599FF";
        }

        private static string _GetMiscColor(Player player)
        {
            return player.Role.Type == RoleTypeId.Tutorial ? "#FF4444" : "#CCCCCC";
        }

        // ==================== 积分通知 ====================

        private void ShowPointsNotif(Player player, string text)
        {
            try
            {
                if (player == null) return;
                // 积分通知已整合到统一显示中，此处仅做日志
                Log.Info($"[积分] {player.Nickname}: {text}");
            }
            catch { }
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
            _killFeed.Clear();
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
