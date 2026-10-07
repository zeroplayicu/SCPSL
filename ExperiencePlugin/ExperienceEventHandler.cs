using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using InventorySystem.Items;
using InventorySystem.Items.Keycards;
using Interactables.Interobjects.DoorUtils;
using MapGeneration.Distributors;
using CustomPlayerEffects;
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
        private const int MaxKillFeed = 20;       // 最多同时显示20条
        private const int KillFeedLifetime = 15;   // 每条最多显示15秒，到期被后面的顶上去
        private const int MaxHintBytes = 8000;

        // ===== VIP头衔刷新计数器 =====
        private int _vipRefreshCounter = 0;

        // ===== 层文本缓存（内容未变跳过 ShowHint，减少网络请求） =====
        // key: "UserId|layerId", value: 上次发送的文本
        private readonly Dictionary<string, string> _lastLayerTexts = new Dictionary<string, string>();
        // key: "UserId|layerId", value: 该层最后一次发送时间（用于持久层到期强制重发）
        private readonly Dictionary<string, DateTime> _lastLayerSentTime = new Dictionary<string, DateTime>();
        // 持久层（状态栏/BC/击杀播报/战斗等）超过此秒数后即使内容未变也强制重发，
        // 防止 HSM 的 Hint 过期/玩家重连后层永久消失。需远小于持久 Hint 时长(30s)，
        // 建议 8-12 秒，保证 UI 短暂消失后能快速恢复。
        private const double PersistentResendInterval = 10;

        public ExperienceEventHandler(ExperiencePlugin plugin) { _plugin = plugin; }

        // ==================== 玩家加入 ====================

        public void OnVerified(VerifiedEventArgs ev)
        {
            try
            {
                var data = _plugin.DataManager.GetOrCreatePlayerData(ev.Player);
                SetVipBadge(ev.Player, data);
                // 发送服务器专属设置面板
                _plugin.Sss?.SendToPlayer(ev.Player);
                // 重连后恢复玩家头顶文字（DisplayNickname 会被游戏重置为默认昵称）
                _plugin.HeadText?.Reapply(ev.Player);

                // 延迟重刷徽章：AdminTools 也在 Verified 时设置 serverRoles.Group，
                // 其 BadgeText 置空后可能覆盖 RankName 显示。延迟 1.5s 再刷一次确保玩家列表徽章最终生效。
                Timing.CallDelayed(1.5f, () =>
                {
                    try
                    {
                        if (ev.Player == null || !ev.Player.IsConnected) return;
                        var d = _plugin.DataManager.GetPlayerData(ev.Player.UserId);
                        if (d != null) SetVipBadge(ev.Player, d);
                    }
                    catch { }
                });
            }
            catch (Exception ex) { Log.Error($"加入: {ex.Message}"); }
        }

        /// <summary>公共方法：刷新单个玩家的 VIP/Admin 头衔（供 SssSettings 等模块调用）</summary>
        public void SetVipBadge(Player player, PlayerData data)
        {
            try
            {
                if (player == null || data == null) return;
                if (string.IsNullOrEmpty(player.UserId)) return;

                // 只设置 RankName（用于TAB玩家列表的Badge列），不要碰 CustomName
                // 否则 CustomName + RankName 会被拼接导致重复显示

                // 拼接管理员称号(从 AdminTools 查询)——纯文本，玩家列表渲染不支持富文本标签
                string adminTitle = GetAdminTitle(player.UserId);
                string adminPart = string.IsNullOrEmpty(adminTitle) ? "" : $" {adminTitle}";

                // 所有玩家：清除 serverRoles.Group.BadgeText，避免其覆盖 RankName 显示（Group 保留用于权限）
                ClearGroupBadgeText(player);

                // 防呆: LevelPrefix 为空时用默认值
                string lvlPrefix = string.IsNullOrEmpty(_plugin.Config.LevelPrefix) ? "Lv." : _plugin.Config.LevelPrefix;

                string rankText;
                if (data.IsLaborReform)
                {
                    rankText = $"【劳改中】 {lvlPrefix}{data.Level}{adminPart}";
                }
                else if (!data.ShowVipTitle)
                {
                    // 即使隐藏VIP标题，也显示等级前缀和管理员称号
                    rankText = $"{lvlPrefix}{data.Level}{adminPart}";
                }
                else
                {
                    string lvText = $" {lvlPrefix}{data.Level}";
                    if (data.VipLevel >= 2)
                        rankText = "【SVIP】" + lvText + adminPart;
                    else if (data.VipLevel >= 1)
                        rankText = "【VIP】" + lvText + adminPart;
                    else
                        rankText = lvText.TrimStart() + adminPart;
                }

                // 设置玩家列表徽章文本 + 颜色（颜色按 管理员 > SVIP > VIP 优先级）
                // 用户要求（2026-10-01 11:13）：删除头顶头衔显示 → RankName/RankColor 清空
                player.RankName = null;
                player.RankColor = null;
                return;
                /*
                string rankColor = null;
                if (!string.IsNullOrEmpty(adminTitle))
                    rankColor = "#FFD700";          // 管理员 金色
                else if (data.VipLevel >= 2)
                    rankColor = "#FF69B4";          // SVIP 粉色
                else if (data.VipLevel >= 1)
                    rankColor = "#FFD700";          // VIP 金色
                if (!string.IsNullOrEmpty(rankColor))
                    player.RankColor = rankColor;
                */
            }
            catch (Exception ex)
            {
                if (_plugin.Config.Debug)
                    Log.Debug($"[VipBadge] 设置失败: {player?.Nickname}: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除玩家 serverRoles.Group 的 BadgeText（保留 Group 本身，admin 玩家权限依赖 Group）。
        /// 原因：玩家列表(TAB/RA)徽章列优先显示 Group.BadgeText，会导致 RankName 里的 VIP/管理头衔被覆盖丢失。
        /// </summary>
        private static void ClearGroupBadgeText(Player player)
        {
            try
            {
                if (player.ReferenceHub == null) return;
                var serverRolesField = typeof(ReferenceHub).GetField("serverRoles",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (serverRolesField == null) return;
                var serverRoles = serverRolesField.GetValue(player.ReferenceHub);
                if (serverRoles == null) return;
                var groupProp = serverRoles.GetType().GetProperty("Group",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (groupProp == null) return;
                var currentGroup = groupProp.GetValue(serverRoles);
                if (currentGroup != null)
                {
                    var badgeTextProp = currentGroup.GetType().GetProperty("BadgeText");
                    var badgeColorProp = currentGroup.GetType().GetProperty("BadgeColor");
                    bool needChange = false;
                    if (badgeTextProp != null)
                    {
                        string bt = badgeTextProp.GetValue(currentGroup) as string;
                        if (!string.IsNullOrEmpty(bt)) { badgeTextProp.SetValue(currentGroup, ""); needChange = true; }
                    }
                    if (badgeColorProp != null)
                    {
                        string bc = badgeColorProp.GetValue(currentGroup) as string;
                        if (!string.IsNullOrEmpty(bc)) { badgeColorProp.SetValue(currentGroup, ""); needChange = true; }
                    }
                    if (needChange)
                    {
                        try { if (ExperiencePlugin.Instance?.Config?.Debug == true) Log.Debug($"[VipBadge] 已清空 {player.Nickname} 的 Group BadgeText/BadgeColor，改由 RankName 显示头衔"); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>从 AdminTools 插件获取该玩家的管理员称号(lv3-6)，无则返回空</summary>
        private static string GetAdminTitle(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId)) return "";
                // 通过反射查找 AdminTools.Instance.Manager.GetAdminLevel
                var adminAsm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "AdminTools");
                if (adminAsm == null) return "";
                var instType = adminAsm.GetType("AdminTools.AdminTools");
                if (instType == null) return "";
                var instProp = instType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var inst = instProp?.GetValue(null);
                if (inst == null) return "";
                var mgrProp = instType.GetProperty("Manager");
                var mgr = mgrProp?.GetValue(inst);
                if (mgr == null) return "";
                var getLevelMethod = mgr.GetType().GetMethod("GetAdminLevel", new[] { typeof(string) });
                if (getLevelMethod == null) return "";
                int level = (int)getLevelMethod.Invoke(mgr, new object[] { userId });
                if (level < 3) return "";
                string title = level switch
                {
                    6 => "服主",
                    5 => "高级admin",
                    4 => "普通admin",
                    3 => "见习admin",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(title))
                {
                    try { if (ExperiencePlugin.Instance?.Config?.Debug == true) Log.Debug($"[AdminTitle] {userId} -> lv{level} '{title}'"); } catch { }
                }
                return title;
            }
            catch (Exception ex)
            {
                try { if (ExperiencePlugin.Instance?.Config?.Debug == true) Log.Debug($"[AdminTitle] 反射失败: {ex.GetType().Name}: {ex.Message}"); } catch { }
                return "";
            }
        }

        /// <summary>公共方法：刷新单个玩家的 VIP 头衔（供命令调用）</summary>
        public void RefreshSingleVipBadge(Player player, PlayerData data)
        {
            SetVipBadge(player, data);
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

                // 撤离触发的换角色：让游戏默认撤离奖励角色正常生效，跳过劳改与开局 buff
                // （修复：撤离后变观察者 —— 原先在 Spawning 事件内同步 Role.Set 重入，会中止撤离角色变更）
                if (ev.Reason == SpawnReason.Escaped)
                {
                    Log.Info($"[撤离] {player.Nickname} 撤离角色生效: {player.Role.Type}");
                    return;
                }

                // 刷新头衔（VIP/劳改）
                SetVipBadge(player, data);

                // 重生/换角色后恢复头顶文字（游戏可能重置 DisplayNickname）
                _plugin.HeadText?.Reapply(player);

                // 劳改玩家：强制 D级 + 减速 + 发硬币，不发放等级 buff
                if (data.IsLaborReform)
                {
                    // Spawned（角色管线完成后）再 Set 是安全做法；不传 SpawnFlags 即默认 All（血量/背包正常）
                    if (player.Role.Type != RoleTypeId.ClassD)
                        player.Role.Set(RoleTypeId.ClassD, SpawnReason.ForceClass);
                    ApplyLaborReform(player, data);
                    return;
                }

                int level = data.Level;

                // → 等级增幅：非D级（保安/博士）出生，等级超过 SpawnColaLevel(50) 给1瓶可乐(SCP207)
                if (!player.IsScp && _plugin.Config.EnableHumanLevelBuff)
                {
                    var role = player.Role.Type;
                    bool isGuardOrScientist = role == RoleTypeId.FacilityGuard ||
                                              role == RoleTypeId.Scientist;

                    if (isGuardOrScientist && level > _plugin.Config.SpawnColaLevel)
                    {
                        GiveScp207(player, 1);
                        Log.Info($"[Buff] {player.Nickname} 等级{level} → 保安/博士开局可乐×1");
                    }
                }

                // D级人员加开局物品
                if (player.Role.Type == RoleTypeId.ClassD)
                {
                    if (Enum.TryParse<ItemType>(_plugin.Config.ClassDItem, out var itemType))
                        player.AddItem(itemType);
                }
            }
            catch (Exception ex) { Log.Error($"生成buff: {ex.Message}"); }
        }

        // ==================== 撤离经验 ====================

        /// <summary>撤离成功 — 发放撤离经验/积分（修复：撤离后无法获得经验）</summary>
        public void OnEscaped(EscapedEventArgs ev)
        {
            try
            {
                if (ev.Player == null || ev.Player.IsNPC || string.IsNullOrEmpty(ev.Player.UserId)) return;

                int expGain = _plugin.Config.ExpPerEscape;
                bool leveledUp = _plugin.DataManager.AddExperience(ev.Player, expGain);
                float ptsGained = _plugin.DataManager.AddPoints(ev.Player.UserId, _plugin.Config.PointsPerEscape);
                ShowPointsNotif(ev.Player, $"+{ptsGained:F1} 积分 (撤离)");

                if (!CombatDataCache.TryGetValue(ev.Player.UserId, out CombatData cd))
                {
                    cd = new CombatData();
                    CombatDataCache[ev.Player.UserId] = cd;
                }

                string settleText = $"撤离成功 +{expGain}xp";
                if (leveledUp)
                {
                    var data = _plugin.DataManager.GetPlayerData(ev.Player.UserId);
                    if (data != null)
                        settleText += $"\n升级！{_plugin.Config.LevelPrefix}{data.Level}";
                }
                cd.SettlePendingText = settleText;
                cd.SettlePendingColor = "lime";
                cd.SettlePendingTime = DateTime.Now.AddSeconds(7);
                cd.LastFeedTime = DateTime.Now;

                Log.Info($"[撤离] {ev.Player.Nickname} 撤离成功 +{expGain}xp +{ptsGained:F1}积分");
                RefreshPlayerPanel(ev.Player);
            }
            catch (Exception ex) { Log.Error($"撤离经验: {ex.Message}"); }
        }

        // ==================== 劳改系统 ====================

        /// <summary>应用劳改效果（减速 + 发硬币）</summary>
        public void ApplyLaborReform(Player player, PlayerData data)
        {
            try
            {
                if (player == null || data == null) return;

                // 减速效果：使用 SinkHole 降低移速
                player.EnableEffect(EffectType.SinkHole, 9999f, false);
                Log.Info($"[劳改] {player.Nickname} 移速已减少{_plugin.Config.LaborReformSpeedReduction}%");

                // 发放硬币（无法丢弃）
                for (int i = 0; i < _plugin.Config.LaborReformCoinCount; i++)
                    player.AddItem(ItemType.Coin);

                // 刷新头衔显示
                SetVipBadge(player, data);

                Log.Info($"[劳改] {player.Nickname} 已应用劳改效果");
            }
            catch (Exception ex) { Log.Error($"劳改应用: {ex.Message}"); }
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

        // ==================== 死亡不掉弹药 + 清空枪膛 ====================

        public void OnDying(DyingEventArgs ev)
        {
            try
            {
                // SCP自选：检测SCP死亡
                var role = ev.Player.Role;
                if (role != null && role.Type.ToString().StartsWith("Scp"))
                    _plugin.ScpSelect.OnScpDied(ev.Player, role.Type);

                // 清空备弹（使用 EXILED SetAmmo API）
                foreach (var ammoType in Enum.GetValues(typeof(AmmoType)))
                {
                    try { ev.Player.SetAmmo((AmmoType)ammoType, (ushort)0); }
                    catch { }
                }
                UnloadAllFirearms(ev.Player);
            }
            catch (Exception ex) { Log.Error($"Dying: {ex.Message}"); }
        }

        /// <summary>击杀经验：按被击杀者阵营（2026-10-06）</summary>
        private int GetKillExpByVictim(Player victim)
        {
            try
            {
                string n = victim.Role.Type.ToString();
                if (n.StartsWith("Ntf")) return _plugin.Config.ExpPerKillMtf;          // 机动特遣队 → 100
                if (n == "ClassD") return _plugin.Config.ExpPerKillClassD;            // D 级 → 100
                if (n == "Tutorial") return _plugin.Config.ExpPerKillGoc;             // GOC（Tutorial 载体）→ 350
            }
            catch { }
            return _plugin.Config.ExpPerKill;
        }

        /// <summary>被击杀者阵营简称（用于击杀提示）</summary>
        private static string GetVictimLabel(Player victim)
        {
            try
            {
                if (victim.IsScp) return "SCP";
                string n = victim.Role.Type.ToString();
                if (n.StartsWith("Ntf")) return "MTF";
                if (n == "ClassD") return "DD";
                if (n == "Tutorial") return "GOC";
                if (n.StartsWith("Chaos")) return "混沌";
                if (n == "Scientist") return "科研";
                if (n == "FacilityGuard") return "设施安保";
                return n;
            }
            catch { return "目标"; }
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

        /// <summary>SCP-207（可乐）无伤：拦截 207 的周期性扣血，保留移速/回血等正面效果（exp 插件功能）</summary>
        public void OnHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                if (ev.DamageHandler != null && ev.DamageHandler.Type == Exiled.API.Enums.DamageType.Scp207)
                    ev.IsAllowed = false;
            }
            catch { }
        }

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

                // === 1. 助攻伤害追踪 ===
                if (!_assistDamage.TryGetValue(victimId, out var attackerDict))
                {
                    attackerDict = new Dictionary<string, int>();
                    _assistDamage[victimId] = attackerDict;
                }
                if (!attackerDict.TryGetValue(attackerId, out int prevDamage))
                    prevDamage = 0;
                attackerDict[attackerId] = prevDamage + damage;

                // === 2. 组伤检测 ===
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
                    Log.Debug($"[伤害] {ev.Attacker.Nickname}: {damage} 伤害 (仅追踪助攻)");
            }
            catch (Exception ex) { Log.Error($"伤害事件: {ex.Message}"); }
        }

        // ==================== 无限备弹 ====================
        // 逻辑：
        //   手持枪械 → 背包(备用)子弹强制锁定为 InfiniteAmmoCount(120)
        //   换弹/拾取 → 补满到 120
        //   死亡 → 清空备弹 + 清空枪膛
        //   地面 → 每15秒自动删除弹药掉落物

        private DateTime _lastAmmoCleanup = DateTime.MinValue;
        private const int AmmoCleanupInterval = 15; // 秒

        /// <summary>根据武器类型名称返回对应弹药类型 (AmmoType)</summary>
        private static AmmoType GetAmmoTypeForWeapon(string weaponName)
        {
            if (weaponName.IndexOf("E11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FRMG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("FSP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("A7", StringComparison.OrdinalIgnoreCase) >= 0)
                return AmmoType.Nato556;

            if (weaponName.IndexOf("AK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Logicer", StringComparison.OrdinalIgnoreCase) >= 0)
                return AmmoType.Nato762;

            // 霰弹枪用 12号 口径弹药（原来是Nato9，导致霰弹枪无法无限备弹）
            if (weaponName.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0)
                return AmmoType.Ammo12Gauge;

            // 左轮手枪用 .44 口径弹药（原来是Nato9，导致左轮备弹类型错误）
            if (weaponName.IndexOf("Revolver", StringComparison.OrdinalIgnoreCase) >= 0)
                return AmmoType.Ammo44Cal;

            // 手枪/冲锋枪用 9mm
            if (weaponName.IndexOf("COM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weaponName.IndexOf("Crossvec", StringComparison.OrdinalIgnoreCase) >= 0)
                return AmmoType.Nato9;

            return AmmoType.None;
        }

        /// <summary>通过反射获取枪械弹匣容量（稳健三层回退）</summary>
        private static int GetFirearmMaxAmmo(object firearmItem)
        {
            try
            {
                var baseProp = firearmItem.GetType().GetProperty("Base");
                if (baseProp == null) return 30;
                var firearmBase = baseProp.GetValue(firearmItem);
                if (firearmBase == null) return 30;
                var fbType = firearmBase.GetType();

                // 优先：AmmoManagerModule.MaxAmmo 属性
                try
                {
                    var ammoMgrField = fbType.GetField("AmmoManagerModule", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (ammoMgrField != null)
                    {
                        var ammoMgr = ammoMgrField.GetValue(firearmBase);
                        if (ammoMgr != null)
                        {
                            var maxAmmoProp = ammoMgr.GetType().GetProperty("MaxAmmo", BindingFlags.Public | BindingFlags.Instance);
                            if (maxAmmoProp != null)
                            {
                                int val = (int)maxAmmoProp.GetValue(ammoMgr);
                                if (val > 0) return val;
                            }
                        }
                    }
                }
                catch { }

                // 次选：Status.Ammo 的默认容量（通过 ActionName 映射）
                try
                {
                    var actionNameProp = fbType.GetProperty("ActionName");
                    if (actionNameProp != null)
                    {
                        var actionName = actionNameProp.GetValue(firearmBase)?.ToString();
                        if (!string.IsNullOrEmpty(actionName))
                        {
                            if (actionName.IndexOf("E11", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
                            if (actionName.IndexOf("FRMG", StringComparison.OrdinalIgnoreCase) >= 0) return 60;
                            if (actionName.IndexOf("FSP", StringComparison.OrdinalIgnoreCase) >= 0) return 30;
                            if (actionName.IndexOf("AK", StringComparison.OrdinalIgnoreCase) >= 0) return 30;
                            if (actionName.IndexOf("Logicer", StringComparison.OrdinalIgnoreCase) >= 0) return 100;
                            if (actionName.IndexOf("COM15", StringComparison.OrdinalIgnoreCase) >= 0) return 12;
                            if (actionName.IndexOf("COM18", StringComparison.OrdinalIgnoreCase) >= 0) return 15;
                            if (actionName.IndexOf("COM20", StringComparison.OrdinalIgnoreCase) >= 0) return 10;
                            if (actionName.IndexOf("Crossvec", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
                            if (actionName.IndexOf("Revolver", StringComparison.OrdinalIgnoreCase) >= 0) return 6;
                            if (actionName.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0) return 14;
                            if (actionName.IndexOf("A7", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
                        }
                    }
                }
                catch { }

                // 最后：Status.MaxAmmo 字段
                var statusProp = fbType.GetProperty("Status");
                if (statusProp != null)
                {
                    var status = statusProp.GetValue(firearmBase);
                    if (status != null)
                    {
                        var maxAmmoField = status.GetType().GetField("MaxAmmo");
                        if (maxAmmoField != null)
                        {
                            int val = (int)maxAmmoField.GetValue(status);
                            if (val > 0) return val;
                        }
                    }
                }
            }
            catch { }
            return 30;
        }

        /// <summary>
        /// 通过反射把手持枪械的弹匣(当前已装填子弹)补满到最大容量，填满所有空余空间。
        /// 采用与 GetFirearmMaxAmmo 一致的方式访问 AmmoManagerModule.Ammo。
        /// </summary>
        private static void RefillFirearmMagazine(object firearmItem)
        {
            try
            {
                if (firearmItem == null) return;

                var baseProp = firearmItem.GetType().GetProperty("Base");
                if (baseProp == null) return;
                var firearmBase = baseProp.GetValue(firearmItem);
                if (firearmBase == null) return;
                var fbType = firearmBase.GetType();

                // AmmoManagerModule.Ammo (当前弹匣子弹) 属性 → 设置为 MaxAmmo
                var ammoMgrField = fbType.GetField("AmmoManagerModule", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ammoMgrField == null) return;
                var ammoMgr = ammoMgrField.GetValue(firearmBase);
                if (ammoMgr == null) return;
                var mgrType = ammoMgr.GetType();

                var ammoProp = mgrType.GetProperty("Ammo", BindingFlags.Public | BindingFlags.Instance);
                if (ammoProp == null) return;
                var maxAmmoProp = mgrType.GetProperty("MaxAmmo", BindingFlags.Public | BindingFlags.Instance);
                if (maxAmmoProp == null) return;

                int currentMag = (int)ammoProp.GetValue(ammoMgr);
                int maxAmmo = (int)maxAmmoProp.GetValue(ammoMgr);
                if (maxAmmo > 0 && currentMag < maxAmmo)
                {
                    ammoProp.SetValue(ammoMgr, (ushort)maxAmmo);
                }
            }
            catch { }
        }

        /// <summary>
        /// 手持枪械时，把该枪对应的背包(备用)子弹强制补满到 InfiniteAmmoCount。
        /// 换弹后补到 120，保证玩家一直有子弹。
        /// </summary>
        private void GiveAmmoForAllFirearms(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected) return;

                // 只处理当前手持的物品
                var current = player.CurrentItem;
                if (current == null) return;

                string typeName = current.Type.ToString();
                bool isGun = typeName.Contains("Gun") || typeName.Contains("Revolver") || typeName.Contains("Crossvec");
                if (!isGun) return;

                var ammoType = GetAmmoTypeForWeapon(typeName);
                if (ammoType == AmmoType.None) return;

                int target = Math.Max(1, _plugin.Config.InfiniteAmmoCount);

                // 背包(备用)子弹补满到 120
                // 用 SetAmmo 而非 AddAmmo：AddAmmo 超过物品弹药容量上限会把多余子弹掉落成脚底拾取物(切换枪械时出现子弹)
                // SetAmmo 直接设定精确数量，不会掉落多余子弹
                int currentAmmo = player.GetAmmo(ammoType);
                if (currentAmmo < target)
                {
                    player.SetAmmo(ammoType, (ushort)target);
                }

                // 同时把手持枪械的弹匣补满到最大容量，填满所有空余空间
                RefillFirearmMagazine(current);

                if (_plugin.Config.Debug)
                    Log.Debug($"[备弹] {player.Nickname} 手持{typeName} → 备用{ammoType}={player.GetAmmo(ammoType)}, 弹匣已补满");
            }
            catch (Exception ex) { Log.Error($"GiveAmmo: {ex.Message}"); }
        }

        // ---- 换弹时给子弹 ----

        public void OnReloadingWeapon(ReloadingWeaponEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;
                GiveAmmoForAllFirearms(ev.Player);
            }
            catch (Exception ex) { Log.Error($"换弹: {ex.Message}"); }
        }

        // ---- 拾取枪械时给子弹 ----

        public void OnItemAdded(ItemAddedEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.EnableInfiniteAmmo) return;
                if (ev.Item == null || ev.Pickup == null) return; // 仅处理拾取

                var player = ev.Player;
                if (player == null) return;

                string typeName = ev.Item.Type.ToString();
                bool isGun = typeName.Contains("Gun") || typeName.Contains("Revolver") || typeName.Contains("Crossvec");
                if (!isGun) return;

                GiveAmmoForAllFirearms(player);
            }
            catch (Exception ex) { Log.Error($"拾取: {ex.Message}"); }
        }

        // ---- 丢枪清零弹药 ----

        public void OnDroppingItem(DroppingItemEventArgs ev)
        {
            try
            {
                // 用户要求（2026-10-02）：禁止丢弃子弹（弹药类物品一律不能丢）
                if (IsAmmoItem(ev.Item.Type))
                {
                    ev.IsAllowed = false;
                    ev.Player.ShowHint("<color=#FF4444>[系统] 子弹不能丢弃</color>", 1.5f);
                    return;
                }

                // 劳改玩家禁止丢弃硬币
                var dropData = _plugin.DataManager.GetPlayerData(ev.Player.UserId);
                if (dropData != null && dropData.IsLaborReform && ev.Item.Type == ItemType.Coin)
                {
                    ev.IsAllowed = false;
                    return;
                }

                if (!_plugin.Config.EnableInfiniteAmmo) return;

                string itemName = ev.Item.Type.ToString();
                if (!itemName.Contains("Gun") && !itemName.Contains("Micro") && !itemName.Contains("Disruptor"))
                    return;

                // 丢枪：清空该枪枪膛 + 所有备弹
                UnloadSpecificFirearm(ev.Item);
                foreach (var ammoType in ev.Player.Ammo.Keys.ToList())
                    ev.Player.ReferenceHub.inventory.UserInventory.ReserveAmmo[ammoType] = 0;
            }
            catch (Exception ex) { Log.Error($"丢枪: {ex.Message}"); }
        }

        /// <summary>判断是否为弹药类物品（ItemType.Ammo* 全部视为子弹）</summary>
        public static bool IsAmmoItem(ItemType type)
        {
            string n = type.ToString();
            return n.StartsWith("Ammo");
        }

        /// <summary>死亡时清空弹药：禁止死亡掉落子弹（用户要求 2026-10-02）</summary>
        public void ClearAmmoOnDying(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected) return;
                foreach (var ammoType in player.Ammo.Keys.ToList())
                    player.ReferenceHub.inventory.UserInventory.ReserveAmmo[ammoType] = 0;
            }
            catch (Exception ex) { Log.Debug($"死亡清弹失败: {ex.Message}"); }
        }

        private static void UnloadSpecificFirearm(Exiled.API.Features.Items.Item item)
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

                // 确定真正的击杀者：口袋维度死亡时 ev.Attacker 可能为 null
                Player killer = ev.Attacker;
                string killWeaponOverride = null;
                bool isPocketKill = false;
                if ((killer == null || killer == ev.Player) && ev.DamageHandler != null)
                {
                    try
                    {
                        string dhName = ev.DamageHandler.GetType().Name;
                        if (dhName.Contains("PocketDimension") || dhName.Contains("Pocket"))
                        {
                            killer = Player.List.FirstOrDefault(p =>
                                p.IsAlive && p.Role.Type == RoleTypeId.Scp106);
                            killWeaponOverride = "口袋维度";
                            isPocketKill = true;
                            if (killer != null)
                                Log.Info($"[口袋维度] {ev.Player.Nickname} → SCP-106 {killer.Nickname} 击杀");
                        }
                    }
                    catch { }
                }

                if (killer != null && killer != ev.Player)
                {
                    string killerId = killer.UserId;

                    // 计算击杀经验：口袋维度击杀给 SCP-106 满额击杀经验
                    int killExp;
                    bool victimIsScp = ev.Player.IsScp;
                    if (isPocketKill)
                    {
                        // SCP-106 入口袋击杀：给 2 倍击杀经验
                        killExp = _plugin.Config.ExpPerKill * 2;
                    }
                    else if (victimIsScp)
                    {
                        int killerDamage = 0;
                        if (_assistDamage.TryGetValue(victimId, out var attackerDict))
                            attackerDict.TryGetValue(killerId, out killerDamage);

                        float scpMaxHp = 0f;
                        try { scpMaxHp = ev.Player.MaxHealth; } catch { }
                        if (scpMaxHp > 0 && killerDamage > 0)
                        {
                            // 1%伤害 = ScpExpPerPercent(10)经验 → (damage / maxHp) * 100 * ratio
                            killExp = (int)(killerDamage * 100 * _plugin.Config.ScpExpPerPercent / scpMaxHp);
                            // 防御：SCP击杀经验不能为负或过大，避免后续升级循环卡死/崩溃
                            if (killExp < 0) killExp = _plugin.Config.ExpPerKill;
                            if (killExp > 100000) killExp = 100000;
                        }
                        else
                        {
                            killExp = _plugin.Config.ExpPerKill; // 保底
                        }
                    }
                    else
                    {
                        // 2026-10-06 用户要求：按被击杀者阵营给经验
                        //   击杀 MTF / DD = 100xp，击杀 GOC = 350xp，其余沿用默认
                        killExp = GetKillExpByVictim(ev.Player);
                    }

                    bool leveledUp = _plugin.DataManager.AddExperience(killer, killExp);

                    // 击杀提示（2026-10-06 用户要求）：如"击杀MTF获得100xp"
                    if (!victimIsScp || isPocketKill)
                    {
                        killer.ShowHint($"<color=#FFD700>击杀 <color=#FFFFFF>{GetVictimLabel(ev.Player)}</color> 获得 <color=#00FF88>{killExp} xp</color></color>", 3f);
                    }
                    _plugin.DataManager.AddKill(killerId);
                    float ptsGained = _plugin.DataManager.AddPoints(killerId, _plugin.Config.PointsPerKill);
                    ShowPointsNotif(killer, $"+{ptsGained:F1} 积分 (击杀)");
                    AddRoundKill(killerId);

                    if (!CombatDataCache.TryGetValue(killerId, out CombatData cd))
                    {
                        cd = new CombatData();
                        CombatDataCache[killerId] = cd;
                    }
                    cd.KillStreak++;
                    cd.HasKillExp = true;
                    cd.KillExpAwarded = killExp;
                    cd.LastFeedTime = DateTime.Now;
                    // 设置积分通知（右侧显示）
                    string ptsLabel = isPocketKill ? "口袋击杀" : "击杀";
                    cd.PointsNotifyText = $"<color=#FFD700>积分 +{ptsGained:F1}</color>\n<color=#AA66FF>{ptsLabel}</color>";
                    cd.PointsNotifyTime = DateTime.Now.AddSeconds(8);
                    RefreshPlayerPanel(killer);

                    if (leveledUp)
                    {
                        var data = _plugin.DataManager.GetPlayerData(killerId);
                        // 通过 CombatData 统一显示，避免重叠
                        if (!CombatDataCache.TryGetValue(killerId, out CombatData cdLvl))
                        {
                            cdLvl = new CombatData();
                            CombatDataCache[killerId] = cdLvl;
                        }
                        cdLvl.SettlePendingText = "升级！{level}".Replace("{level}", _plugin.Config.LevelPrefix + data.Level);
                        cdLvl.SettlePendingColor = "lime";
                        cdLvl.SettlePendingTime = DateTime.Now.AddSeconds(7);
                        cdLvl.LastFeedTime = DateTime.Now;
                    }

                    // ---- 击杀特效（三角洲风格：常规=白色X，爆头/一击必杀=骷髅） ----
                    if (_plugin.Config.EnableKillEffect)
                    {
                        bool specialKill = !string.IsNullOrEmpty(GetSpecialKillTag(ev.DamageHandler, killer));
                        ShowKillEffect(killer, cd.KillStreak, specialKill);
                    }
                }

                ProcessAssists(ev, killer?.UserId ?? (ev.Attacker != null && ev.Attacker != ev.Player ? ev.Attacker.UserId : null));

                if (killer != null && killer != ev.Player)
                {
                    try
                    {
                        string weaponName = killWeaponOverride ?? GetWeaponDisplayName(ev.DamageHandler);
                        string specialTag = GetSpecialKillTag(ev.DamageHandler, killer);
                        string killerColor = GetRoleColor(killer);
                        string victimColor = GetRoleColor(ev.Player);
                        string weaponIcon = killWeaponOverride != null ? "🌀" : "🔫";
                        // 截断过长的玩家名（避免长名字+多行堆叠导致屏幕溢出/重叠）
                        string killerName = Truncate(killer.Nickname, 16);
                        string victimName = Truncate(ev.Player.Nickname, 16);
                        string killMsg = $"<color={killerColor}>{killerName}</color>" +
                            $"<color=white> {specialTag}{weaponIcon} </color>" +
                            $"<color={victimColor}>{victimName}</color>";

                        // 去重：若最近 1.5 秒内已有相同 killer+weapon+victim 组合，则不重复入堆
                        // （OnDied 在多玩家同时死亡或连环击杀时可能多次触发同一事件，避免刷屏）
                        string dedupKey = $"{killer.UserId}|{weaponIcon}|{ev.Player.UserId}";
                        DateTime dedupCutoff = DateTime.Now.AddSeconds(-1.5);
                        bool duplicate = _killFeed.Any(x => x.Time >= dedupCutoff && x.Text == killMsg);
                        if (!duplicate)
                        {
                            _killFeed.Add((killMsg, DateTime.Now));
                            while (_killFeed.Count > MaxKillFeed)
                                _killFeed.RemoveAt(0);
                        }

                        // 刷新所有玩家的击杀播报
                        RefreshKillFeed();
                    }
                    catch { }
                }

                if (killer != null && killer != ev.Player &&
                    killer.Role.Team == ev.Player.Role.Team && !ev.Player.IsScp)
                {
                    string atkId = killer.UserId;
                    if (!CombatDataCache.TryGetValue(atkId, out CombatData cdTk))
                    {
                        cdTk = new CombatData();
                        CombatDataCache[atkId] = cdTk;
                    }
                    cdTk.SettlePendingText = "击杀队友 -200xp";
                        cdTk.SettlePendingColor = "#FF4444";
                    cdTk.SettlePendingTime = DateTime.Now.AddSeconds(7);
                    cdTk.LastFeedTime = DateTime.Now;
                }

                DestroyAmmoPickups();
            }
            catch (Exception ex) { Log.Error($"死亡: {ex.Message}"); }
        }

        private void ProcessAssists(DiedEventArgs ev, string overrideKillerId = null)
        {
            string victimId = ev.Player.UserId;

            if (!_assistDamage.TryGetValue(victimId, out var attackerDict)) return;

            string killerId = overrideKillerId ??
                ((ev.Attacker != null && ev.Attacker != ev.Player) ? ev.Attacker.UserId : null);

            foreach (var kvp in attackerDict)
            {
                string attackerId = kvp.Key;
                int damageDealt = kvp.Value;

                if (attackerId == killerId) continue;

                // 只要造成过至少1点伤害就算助攻，固定获得 ExpPerAssist 经验
                if (damageDealt >= _plugin.Config.AssistMinDamage)
                {
                    var assister = Player.List.FirstOrDefault(p => p.UserId == attackerId);
                    if (assister != null)
                    {
                        AddRoundAssist(attackerId);
                        float ptsGained = _plugin.DataManager.AddPoints(attackerId, _plugin.Config.PointsPerAssist);
                        ShowPointsNotif(assister, $"+{ptsGained:F1} 积分 (助攻)");

                        // 助攻积分通知（右侧显示）
                        if (!CombatDataCache.TryGetValue(attackerId, out CombatData cdAssist))
                        {
                            cdAssist = new CombatData();
                            CombatDataCache[attackerId] = cdAssist;
                        }
                        cdAssist.PointsNotifyText = $"<color=#55DD55>助攻 +{ptsGained:F1}积分</color>";
                        cdAssist.PointsNotifyTime = DateTime.Now.AddSeconds(8);
                        cdAssist.LastFeedTime = DateTime.Now;
                        RefreshPlayerPanel(assister);

                        // 固定助攻经验
                        int assistExp = _plugin.Config.ExpPerAssist;
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

        // ==================== 显示消息过期清理 ====================

        public void CheckAndSettleDamage()
        {
            try
            {
                var now = DateTime.Now;
                var toRemove = new List<string>();
                foreach (var kvp in CombatDataCache)
                {
                    var cd = kvp.Value;
                    // 清除过期的结算/升级消息
                    if (!string.IsNullOrEmpty(cd.SettlePendingText) && now > cd.SettlePendingTime)
                        cd.SettlePendingText = "";
                    // 清除过期的积分通知
                    if (!string.IsNullOrEmpty(cd.PointsNotifyText) && now > cd.PointsNotifyTime)
                        cd.PointsNotifyText = "";
                    // 击杀经验显示过期后重置标记（防止 CombatData 累积不清理）
                    if (cd.HasKillExp && (now - cd.LastFeedTime).TotalSeconds > _plugin.Config.FeedDisplayDuration)
                    {
                        cd.HasKillExp = false;
                        cd.KillExpAwarded = 0;
                    }
                    // 惩罚经验过期后清除
                    if (cd.DisplayPenaltyXp > 0 && (now - cd.LastFeedTime).TotalSeconds > _plugin.Config.FeedDisplayDuration)
                        cd.DisplayPenaltyXp = 0;

                    // 没有任何待显示消息 → 清理
                    if (string.IsNullOrEmpty(cd.SettlePendingText) && string.IsNullOrEmpty(cd.PointsNotifyText) && cd.DisplayPenaltyXp <= 0 && !cd.HasKillExp)
                    { toRemove.Add(kvp.Key); continue; }
                }
                foreach (var id in toRemove) CombatDataCache.Remove(id);
            }
            catch (Exception ex) { Log.Error($"结算检查: {ex.Message}"); }
        }

        // ==================== 统一显示刷新 ====================

        /// <summary>timer 调用：每秒统一刷新所有玩家显示</summary>
        public void RefreshAllDisplays()
        {
            try
            {
                Commands.ChatMessageBuffer.TickCountdowns();
                CheckAndSettleDamage();

                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)).ToList())
                {
                    // 手持枪械时持续把背包子弹锁定补到 InfiniteAmmoCount
                    if (_plugin.Config.EnableInfiniteAmmo)
                        GiveAmmoForAllFirearms(player);

                    RefreshDisplay(player);
                }

                // 每30秒刷新一次所有VIP头衔（防止游戏状态变更导致头衔丢失）
                _vipRefreshCounter++;
                if (_vipRefreshCounter >= 30)
                {
                    _vipRefreshCounter = 0;
                    RefreshAllVipBadges();
                }

                // 每15秒清理一次地面弹药掉落物
                if ((DateTime.Now - _lastAmmoCleanup).TotalSeconds >= AmmoCleanupInterval)
                {
                    _lastAmmoCleanup = DateTime.Now;
                    DestroyAmmoPickups();
                }
            }
            catch (Exception ex) { Log.Error($"统一刷新: {ex.Message}"); }
        }

        /// <summary>遍历所有在线玩家刷新VIP头衔，防止游戏状态变更导致丢失</summary>
        private void RefreshAllVipBadges()
        {
            try
            {
                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)).ToList())
                {
                    var data = _plugin.DataManager.GetPlayerData(player.UserId);
                    if (data != null) SetVipBadge(player, data);
                }
                if (_plugin.Config.Debug)
                    Log.Debug("[VIP] 已刷新所有在线玩家头衔");
            }
            catch (Exception ex) { Log.Error($"VIP头衔刷新: {ex.Message}"); }
        }

        // ==================== 五层显示系统 ====================

        /// <summary>
        /// 各层 Y 坐标（屏幕分辨率 1920×1080 参考）：
        ///   bc_display     (Y=15, Left)       — 左上角 BC 消息
        ///   kill_feed      (Y=80, Right)      — 右上角击杀播报
        ///   points_notify  (Y=350, Right)     — 右侧中部积分通知
        ///   combat_display (Y=600, Center)    — 中间偏下 团队聊天+伤害反馈
        ///   status_display (Y=980, Center)    — 最底部角色状态栏
        ///   抽奖/设置      (Y=300~450)         — 由外部命令控制，不与上述冲突
        /// </summary>
        public void RefreshDisplay(Player player)
        {
            try
            {
                if (player == null || string.IsNullOrEmpty(player.UserId)) return;
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                // ---- 第1层：BC消息（左上） ----
                // 用持久Hint保证BC在倒计时期间持续显示（避免 0.6s 过期导致闪烁/显示过快）
                // 注意: Text 只保留 <color>，字号由 FontSize 控制，避免 HSM 大写化内联 <size> 导致该层消失
                string bcText = Commands.ChatMessageBuffer.BuildBcOnly();
                string bcFormatted = string.IsNullOrEmpty(bcText)
                    ? " " : $"<color=#AAAAAA>{bcText}</color>";
                TryShowPersistentHint(player, "bc_display", bcFormatted,
                    new HsmHint { Id = "bc_display", Text = TruncateHint(bcFormatted),
                        FontSize = 15, YCoordinate = 15, Alignment = HintAlignment.Left });

                // ---- 第1.5层：团队聊天（C消息，左上角BC下方，左对齐）----
                // 专用层，避免与战斗层混排重叠/乱码
                // Y=62：BC层(最多2行,Y=15~45,FontSize=15)下方留安全间距，避免不同玩家BC/团队聊天文字重叠
                string teamChatText = BuildTeamChatLine(player);
                string teamChatFormatted = string.IsNullOrEmpty(teamChatText) ? " " : teamChatText;
                TryShowPersistentHint(player, "teamchat_display", teamChatFormatted,
                    new HsmHint { Id = "teamchat_display", Text = TruncateHint(teamChatFormatted),
                        FontSize = 13, YCoordinate = 62, Alignment = HintAlignment.Left });

                // ---- 第2层：击杀播报（右上） ----
                // 用持久Hint(60s)防止击杀消息 0.6s 过期后因文本缓存跳过重发而消失，造成播报闪烁
                // FontSize 18→13: 20条击杀×13px=260px，从Y=80延伸到Y=340，不与下方积分/结算层重叠
                string killText = BuildKillFeedDisplay();
                string killFormatted = string.IsNullOrEmpty(killText) ? " " : killText;
                TryShowPersistentHint(player, "kill_feed", killFormatted,
                    new HsmHint { Id = "kill_feed", Text = TruncateHint(killFormatted),
                        FontSize = 13, YCoordinate = 80, Alignment = HintAlignment.Right });

                // ---- 第3层：战斗反馈（屏幕中部偏下） — 团队聊天 + 击杀提示 + 效果 ----
                // 用短时Hint(1s)实时刷新，确保 buff 剩余秒数每秒变化时 HSM 客户端每次都重新渲染（避免持久Hint静态快照导致"未刷新"）
                string combatText = BuildCombatMainDisplay(player);
                if (!string.IsNullOrEmpty(combatText))
                {
                    TryShowLiveHint(player, "combat_display", combatText,
                        new HsmHint { Id = "combat_display", Text = TruncateHint(combatText),
                            FontSize = 10, YCoordinate = 780, Alignment = HintAlignment.Center }, 1f);
                }

                // ---- 第4层：角色详情（屏幕最底部居中） - 持久显示，仅变化时推送 ----
                // Y 958→925：为 FactionPlugin 的"特殊角色介绍"层（Y=955）腾出空间
                string status = FormatStatusLine(data, player.UserId);
                TryShowPersistentHint(player, "status_display", status,
                    new HsmHint { Id = "status_display", Text = TruncateHint(status),
                        FontSize = 16, YCoordinate = 925, Alignment = HintAlignment.Center });

                // ---- 第4.5层：服务器TPS（独立层，避免拼接到状态栏造成重影/错位） ----
                // 原来用 1.0/Server.Frametime 每帧都变，导致状态栏文本每秒重发2-3次，HSM 渲染时新旧重叠 -> 看起来像乱码/删除线
                // 改用 Server.Tps（服务器平均TPS）并取整，文本稳定，只在TPS整数变化时才重发，彻底解决重叠
                // 注意: HSM 不会处理 Text 里的内联 <size>/<color> 标签（会被大写化无法解析，结果显示字面量 <COLOR=>），所以 TPS 文本用纯文本
                if (_plugin.Config.ShowServerTps)
                {
                    int tps = (int)Math.Round(Server.Tps);
                    if (tps > 1000) tps = 0; // 极端异常值兜底
                    // 纯文本显示，去掉 <color> 包装避免 HSM 大写化后显示字面量
                    string tpsText = $"TPS: {tps}";
                    // 状态栏上移到 925 后，TPS 同步上移避免重叠
                    TryShowPersistentHint(player, "tps_display", tpsText,
                        new HsmHint { Id = "tps_display", Text = tpsText,
                            FontSize = 12, YCoordinate = 900, Alignment = HintAlignment.Center });
                }

                // ---- 第5层：积分通知（屏幕右侧中部） — 击杀/助攻获得积分 ----
                string ptsText = BuildPointsNotify(player.UserId);
                string ptsFormatted = string.IsNullOrEmpty(ptsText) ? " " : ptsText;
                TryShowHint(player, "points_notify", ptsFormatted,
                    new HsmHint { Id = "points_notify", Text = TruncateHint(ptsFormatted),
                        FontSize = 13, YCoordinate = 360, Alignment = HintAlignment.Right });

                // ---- 第6层：结算/升级消息（屏幕中部） — 专用层，避免内联 <size> 标签被错误处理造成字面量显示 ----
                // 原战斗层中 <size=28><color=lime>升级！Lv.118</color></size> 渲染时被转成 <SIZE=28><COLOR=LIME> 字面量
                // 改用独立层 + 字号由 FontSize 控制 + 仅用 <color> 上色（<color> 标签正常解析）
                string settleText = BuildSettleDisplay(player.UserId);
                string settleFormatted = string.IsNullOrEmpty(settleText) ? " " : settleText;
                TryShowHint(player, "settle_display", settleFormatted,
                    new HsmHint { Id = "settle_display", Text = TruncateHint(settleFormatted),
                        FontSize = 26, YCoordinate = 380, Alignment = HintAlignment.Center });

                // ---- 第7层：击杀反馈（屏幕中部居中靠上） — 专用层，避免内联 <size> 标签造成重叠/乱码 ----
                string killFeedText = BuildKillFeedLine(player.UserId);
                string killFeedFormatted = string.IsNullOrEmpty(killFeedText) ? " " : killFeedText;
                TryShowHint(player, "killfeed_display", killFeedFormatted,
                    new HsmHint { Id = "killfeed_display", Text = TruncateHint(killFeedFormatted),
                        FontSize = 14, YCoordinate = 680, Alignment = HintAlignment.Center });

                // ---- 第8层：攻击队友惩罚（击杀反馈上方） — 专用层，与击杀反馈和状态栏拉开距离避免重叠 ----
                string penaltyText = BuildPenaltyLine(player.UserId);
                string penaltyFormatted = string.IsNullOrEmpty(penaltyText) ? " " : penaltyText;
                TryShowHint(player, "penalty_display", penaltyFormatted,
                    new HsmHint { Id = "penalty_display", Text = TruncateHint(penaltyFormatted),
                        FontSize = 14, YCoordinate = 710, Alignment = HintAlignment.Center });
            }
            catch (Exception ex) { Log.Error($"显示刷新: {ex.Message}"); }
        }

        /// <summary>带缓存的 ShowHint，内容未变时不发送，减少网络请求。
        /// 发送前主动 RemoveHint 同 ID 旧 Hint，避免 HSM 同 ID 未替换导致旧 Hint 残留造成文字叠加/重叠。</summary>
        private void TryShowHint(Player player, string layerId, string text, HsmHint hint, float duration = 0.6f)
        {
            string key = $"{player.UserId}|{layerId}";
            if (_lastLayerTexts.TryGetValue(key, out string last) && last == text)
                return; // 内容相同，跳过发送
            _lastLayerTexts[key] = text;
            var disp = PlayerDisplay.Get(player);
            try { disp.RemoveHint(layerId); } catch { }
            disp.ShowHint(hint, duration);
        }

        /// <summary>实时刷新的短时 Hint（buff 状态栏等），每次无条件 RemoveHint 旧 Hint 后 ShowHint 新短时 Hint。
        /// 用于剩余秒数每秒变化的内容，确保 HSM 客户端每次都重新渲染（避免持久 Hint 静态快照导致"未刷新"）。</summary>
        private void TryShowLiveHint(Player player, string layerId, string text, HsmHint hint, float duration = 1f)
        {
            var disp = PlayerDisplay.Get(player);
            try { disp.RemoveHint(layerId); } catch { }
            disp.ShowHint(hint, duration);
        }

        /// <summary>发送永久层的提示（exp栏、状态栏），持续60秒。
        /// 仅在内容变化时发送；但超过 PersistentResendInterval(30秒) 后即使内容未变也会强制重发，
        /// 防止 HSM 的 60 秒 Hint 过期后该层永久消失（"玩着玩着状态栏消失" bug）。
        /// 发送前主动 RemoveHint 同 ID 旧 Hint，避免文字重叠（多次同 ID ShowHint 累积）。</summary>
        private void TryShowPersistentHint(Player player, string layerId, string text, HsmHint hint)
        {
            string key = $"{player.UserId}|{layerId}";
            bool forceSend = false;
            if (_lastLayerSentTime.TryGetValue(key, out DateTime lastSent))
            {
                // 距离上次发送超过阈值则强制重发，避免 Hint 过期后层消失
                if ((DateTime.Now - lastSent).TotalSeconds >= PersistentResendInterval)
                    forceSend = true;
            }
            if (!forceSend)
            {
                if (_lastLayerTexts.TryGetValue(key, out string last) && last == text)
                    return; // 内容相同且未到强制重发时间，跳过发送
            }
            _lastLayerTexts[key] = text;
            _lastLayerSentTime[key] = DateTime.Now;
            var disp = PlayerDisplay.Get(player);
            try { disp.RemoveHint(layerId); } catch { }
            // 时长 30s > 强制重发间隔 10s，确保层永不因 Hint 过期/玩家重连而永久消失
            disp.ShowHint(hint, 30f);
        }

        /// <summary>截断过长的提示文本，防止超过 Mirror 网络包 65534 字节限制</summary>
        private static string TruncateHint(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxHintBytes)
                return text;
            return text.Substring(0, MaxHintBytes) + "…";
        }

        /// <summary>通用字符串截断（用于玩家名/长文本，避免长名字+多行堆叠导致屏幕溢出/重叠）</summary>
        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "…";
        }

        /// <summary>构建击杀播报文本</summary>
        private string BuildKillFeedDisplay()
        {
            var cutoff = DateTime.Now.AddSeconds(-KillFeedLifetime);
            _killFeed.RemoveAll(x => x.Time < cutoff);
            if (_killFeed.Count == 0) return "";
            return string.Join("\n", _killFeed.Select(x => x.Text));
        }

        /// <summary>构建战斗反馈主文本（中间偏下）— 团队聊天 + 战斗反馈 + 效果（不含状态栏）</summary>
        private string BuildCombatMainDisplay(Player player)
        {
            var sb = new StringBuilder();
            string userId = player.UserId;

            // ---- 团队聊天（C消息，仅同阵营可见）----
            // 现由专用层 teamchat_display 渲染（屏幕左上角），不在此显示
            // string teamChat = ...  // 已迁出

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

            if (sb.Length == 0) sb.Append(' '); // 确保非空，避免HSM空白提示残留
            return sb.ToString();
        }

        // ---- 保留旧方法作为内部调用 ----

        private void RefreshPlayerPanel(Player player) => RefreshDisplay(player);

        private void RefreshKillFeed()
        {
            // 击杀后立即刷新所有玩家显示
            try
            {
                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)).ToList())
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
                        sb.AppendLine($"<color=#00FF00>• {name}</color> <color=#AAAAAA>强度{intensity} | 剩余{remaining}秒</color>");
                    }
                    else
                    {
                        sb.AppendLine($"<color=#00FF00>• {name}</color> <color=#AAAAAA>强度{intensity}</color>");
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

        /// <summary>构建团队聊天文本（专用层 teamchat_display，左上角）</summary>
        private string BuildTeamChatLine(Player player)
        {
            try
            {
                if (player == null) return "";
                string msg = Commands.ChatMessageBuffer.BuildTeamMessages(player.Role.Team);
                return msg ?? "";
            }
            catch { return ""; }
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

        /// <summary>构建积分通知文本（右侧显示）</summary>
        private string BuildPointsNotify(string userId)
        {
            if (!CombatDataCache.TryGetValue(userId, out CombatData cd)) return "";
            // PointsNotifyText 为空或已过期 → 不显示
            if (string.IsNullOrEmpty(cd.PointsNotifyText)) return "";
            if (DateTime.Now > cd.PointsNotifyTime)
            {
                cd.PointsNotifyText = ""; // 确保同时清除，防止下次再读到
                return "";
            }
            return cd.PointsNotifyText;
        }

        /// <summary>构建结算/升级消息文本（专用层渲染，避免内联HTML标签问题）</summary>
        private string BuildSettleDisplay(string userId)
        {
            if (!CombatDataCache.TryGetValue(userId, out CombatData cd)) return "";
            if (string.IsNullOrEmpty(cd.SettlePendingText)) return "";
            if (DateTime.Now > cd.SettlePendingTime)
            {
                cd.SettlePendingText = ""; // 过期清空
                return "";
            }
            // 仅用 <color> 上色，<size> 由 HsmHint.FontSize 控制（避免内联 <size> 标签被错误处理）
            string color = string.IsNullOrEmpty(cd.SettlePendingColor) ? "white" : cd.SettlePendingColor;
            return $"<color={color}>{cd.SettlePendingText}</color>";
        }

        private string BuildCombatFeed(string userId)
        {
            if (!CombatDataCache.TryGetValue(userId, out CombatData cd)) return "";

            // 如果没有击杀经验、惩罚经验，且结算消息已过期 → 不显示
            bool hasKillFeed = cd.HasKillExp && (DateTime.Now - cd.LastFeedTime).TotalSeconds <= _plugin.Config.FeedDisplayDuration;
            bool hasPenalty = cd.DisplayPenaltyXp > 0 && (DateTime.Now - cd.LastFeedTime).TotalSeconds <= _plugin.Config.FeedDisplayDuration;
            bool hasSettle = !string.IsNullOrEmpty(cd.SettlePendingText) && DateTime.Now <= cd.SettlePendingTime;

            if (!hasKillFeed && !hasPenalty && !hasSettle) return "";

            var lines = new List<string>();
            // 击杀反馈/攻击队友惩罚现在由专用层渲染（killfeed_display / penalty_display），
            // 避免内联 <size> 标签在战斗层被错误处理造成字面量显示、重叠、乱码
            // 结算/升级消息也由专用 settle_display 层渲染
            return string.Join("\n", lines);
        }

        /// <summary>构建击杀反馈文本（专用层渲染）</summary>
        private string BuildKillFeedLine(string userId)
        {
            if (!CombatDataCache.TryGetValue(userId, out CombatData cd)) return "";
            if (!cd.HasKillExp) return "";
            if ((DateTime.Now - cd.LastFeedTime).TotalSeconds > _plugin.Config.FeedDisplayDuration) return "";

            string msg = _plugin.Config.KillFeedMessage
                .Replace("{exp}", cd.KillExpAwarded.ToString())
                .Replace("{streak}", cd.KillStreak.ToString());
            // 口袋维度击杀附加标记（无 <size>，字号由专用层 FontSize 控制）
            if (cd.KillExpAwarded > _plugin.Config.ExpPerKill)
                msg += "\n<color=#AA66FF>🌀 口袋维度击杀</color>";
            return msg;
        }

        /// <summary>构建攻击队友惩罚文本（专用层渲染）</summary>
        private string BuildPenaltyLine(string userId)
        {
            if (!CombatDataCache.TryGetValue(userId, out CombatData cd)) return "";
            if (cd.DisplayPenaltyXp <= 0) return "";
            if ((DateTime.Now - cd.LastFeedTime).TotalSeconds > _plugin.Config.FeedDisplayDuration) return "";
            // 无 <size>，字号由专用层 FontSize 控制；<color> 标签可正常解析
            return $"<color=#FF4444>攻击队友 -{cd.DisplayPenaltyXp}xp</color>";
        }

        private string FormatStatusLine(PlayerData data, string userId)
        {
            string msg = _plugin.Config.StatusMessage;
            var player = Player.List.FirstOrDefault(p => p != null && p.UserId == userId);
            string color = GetRoleColor(player);

            // VIP/SVIP徽标：剥离内层 <color> 标签（外层已统一上色，嵌套标签会被 HSM 大写化成字面量显示）
            string vipBadge = "";
            if (data.VipLevel >= 2) vipBadge = StripTags(_plugin.Config.SvipBadge);
            else if (data.VipLevel >= 1) vipBadge = StripTags(_plugin.Config.VipBadge);
            msg = msg.Replace("{vip}", vipBadge);

            msg = msg.Replace("{player}", data.PlayerName);
            msg = msg.Replace("{level}", data.Level.ToString());
            msg = msg.Replace("{exp}", data.Experience.ToString());
            msg = msg.Replace("{maxexp}", data.GetExpForNextLevel(_plugin.Config.BaseExpPerLevel).ToString());
            // {time} 秒级倒计时 → 改为分钟级精度，减少刷新推送频率
            string rawTime = data.GetPlayTimeString();
            // 提取分钟部分（格式如 "12:34"），只有分钟变化时才会触发重发
            string minuteTime = rawTime.Length >= 5 ? rawTime.Substring(0, 5) : rawTime;
            msg = msg.Replace("{time}", minuteTime);
            msg = msg.Replace("{kda}", GetRoundKDAString(userId));
            msg = msg.Replace("{points}", data.Points.ToString("F1"));
            // UID 单独一行（用于 admin 系统 lv3-6 权限管理）。
            // 原来拼在同一行末尾，文本过长时会被屏幕边缘截断，导致积分等信息看不见
            msg += $"\n<color=#FFA500>UID:{data.Uid}</color>";
            // 注意: 不要用内联 <size>（HSM 会大写化无法解析导致经验栏消失），字号由 HsmHint.FontSize 控制
            return $"<color={color}>{msg}</color>";
        }

        /// <summary>剥离富文本标签：HSM 只解析最外层 <color>，内层标签会被大写化成字面量显示</summary>
        private static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]*>", "");
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
                Team.ClassD => "#FFD700",
                _ => _GetMiscColor(player)
            };
        }

        private static string _GetNtfColor(Player player)
        {
            return player.Role.Type == RoleTypeId.FacilityGuard ? "#888888" : "#5599FF";
        }

        private static string _GetMiscColor(Player player)
        {
            return player.Role.Type == RoleTypeId.Tutorial ? "#FF4444" : "#CCCCCC";
        }

        // ==================== 积分通知 ====================

        // ==================== 击杀特效（三角洲风格击杀标记） ====================

        /// <summary>
        /// 击杀特效：参考《三角洲行动》——屏幕中央"闪现"一个简洁击杀标记 + 击杀文字。
        /// 三角洲规则：常规武器击杀显示白色 "X"；一击必杀（爆头/狙击/爆炸）显示骷髅头。
        /// </summary>
        private void ShowKillEffect(Player killer, int killStreak, bool isSpecialKill)
        {
            try
            {
                if (killer == null || !killer.IsConnected) return;
                Timing.RunCoroutine(KillEffectRoutine(killer.UserId, killStreak, isSpecialKill));
            }
            catch (Exception ex) { Log.Error($"击杀特效: {ex.Message}"); }
        }

        /// <summary>击杀标记动画：符号两帧缩放闪现（大→中，共约 0.7 秒）+ 击杀/连杀文字，位置屏幕中央</summary>
        private IEnumerator<float> KillEffectRoutine(string userId, int streak, bool isSpecialKill)
        {
            // 三角洲规则：常规=白色X；特殊击杀(爆头/狙击/爆炸)=红色骷髅
            string mark = isSpecialKill ? "☠" : "X";
            string markColor = isSpecialKill ? "#FF3333" : "#FFFFFF";

            // 连杀提示
            string streakText = null;
            if (streak >= 7) streakText = "GODLIKE !!";
            else if (streak >= 5) streakText = "RAMPAGE !";
            else if (streak == 4) streakText = "MULTI KILL";
            else if (streak == 3) streakText = "TRIPLE KILL";
            else if (streak == 2) streakText = "DOUBLE KILL";

            string text = streakText == null
                ? "<color=#FFD700>击 杀</color>"
                : "<color=#FFD700>击 杀</color>\n<color=#FF5555>" + streakText + "</color>";

            int[] markSizes = { 70, 54 };
            for (int i = 0; i < markSizes.Length; i++)
            {
                var player = Player.Get(userId);
                if (player == null || !player.IsConnected) yield break;
                var disp = PlayerDisplay.Get(player);

                // 击杀标记（中央）
                disp.RemoveHint("kill_mark");
                disp.ShowHint(new HsmHint
                {
                    Id = "kill_mark",
                    Text = $"<color={markColor}>{mark}</color>",
                    FontSize = markSizes[i],
                    YCoordinate = 455,
                    Alignment = HintAlignment.Center
                }, 0.34f);

                // 击杀文字（标记下方）
                disp.RemoveHint("kill_text");
                disp.ShowHint(new HsmHint
                {
                    Id = "kill_text",
                    Text = text,
                    FontSize = 22,
                    YCoordinate = 520,
                    Alignment = HintAlignment.Center
                }, 0.4f);

                yield return Timing.WaitForSeconds(0.33f);
            }

            // 清除
            var last = Player.Get(userId);
            if (last != null && last.IsConnected)
            {
                var d = PlayerDisplay.Get(last);
                d.RemoveHint("kill_mark");
                d.RemoveHint("kill_text");
            }
        }

        /// <summary>特效动画：三帧（大→中→小 + 上飘），MEC 主线程协程执行。
        /// 字号需与图案行数(8行)匹配，避免过大撑满屏幕/与战斗层重叠。</summary>
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

        // ==================== 远程钥匙卡（参考 RemoteKeycard） ====================
        // 背包里有对应权限的钥匙卡即可开门/解锁，无需手持

        /// <summary>门交互事件：若玩家背包中有对应权限的钥匙卡，允许交互</summary>
        public void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.RemoteKeycardAffectDoors) return;
                if (ev.Player == null || ev.Door?.Base == null) return;
                // 门已锁定（含 C.A.S.S.I.E. 锁）时不让远程卡解锁
                if (ev.Door.IsLocked) return;

                if (!ev.IsAllowed && HasKeycardPermission(ev.Player, ev.Door.Base))
                    ev.IsAllowed = true;
            }
            catch (Exception ex) { Log.Error($"远程钥匙卡-门: {ex.Message}"); }
        }

        /// <summary>发电机解锁事件：背包有卡即可解锁</summary>
        public void OnUnlockingGenerator(UnlockingGeneratorEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.RemoteKeycardAffectGenerators) return;
                if (ev.Player == null || ev.Generator?.Base == null) return;

                if (!ev.IsAllowed && HasKeycardPermission(ev.Player, ev.Generator.Base))
                    ev.IsAllowed = true;
            }
            catch (Exception ex) { Log.Error($"远程钥匙卡-发电机: {ex.Message}"); }
        }

        /// <summary>核弹面板激活事件：背包有卡即可激活</summary>
        public void OnActivatingWarheadPanel(ActivatingWarheadPanelEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.RemoteKeycardAffectWarheadPanel) return;
                if (ev.Player == null) return;
                if (AlphaWarheadActivationPanel.Instance == null) return;

                if (!ev.IsAllowed && HasKeycardPermission(ev.Player, AlphaWarheadActivationPanel.Instance))
                    ev.IsAllowed = true;
            }
            catch (Exception ex) { Log.Error($"远程钥匙卡-核弹面板: {ex.Message}"); }
        }

        /// <summary>SCP储物柜交互事件：背包有卡即可交互</summary>
        public void OnInteractingLocker(InteractingLockerEventArgs ev)
        {
            try
            {
                if (!_plugin.Config.RemoteKeycardAffectScpLockers) return;
                if (ev.Player == null || ev.InteractingChamber?.Base == null) return;

                if (!ev.IsAllowed && HasKeycardPermission(ev.Player, ev.InteractingChamber.Base))
                    ev.IsAllowed = true;
            }
            catch (Exception ex) { Log.Error($"远程钥匙卡-SCP储物柜: {ex.Message}"); }
        }

        /// <summary>核心：判断玩家背包中是否有能通过指定门禁需求的钥匙卡</summary>
        private bool HasKeycardPermission(Player player, Interactables.Interobjects.DoorUtils.IDoorPermissionRequester requester)
        {
            try
            {
                if (player == null || requester == null) return false;

                // 失忆症（SCP-008）影响：失忆玩家不能用远程卡
                if (_plugin.Config.RemoteKeycardAmnesiaMatters && player.IsEffectActive<CustomPlayerEffects.AmnesiaItems>())
                    return false;

                // 遍历背包所有物品，找钥匙卡（IDoorPermissionProvider）
                foreach (var item in player.Items)
                {
                    if (item.Base is not Interactables.Interobjects.DoorUtils.IDoorPermissionProvider provider)
                        continue;

                    if (!requester.CheckPermissions(provider, out Interactables.Interobjects.DoorUtils.PermissionUsed callback))
                        continue;

                    // 一次性钥匙卡使用后销毁
                    if (_plugin.Config.RemoteKeycardSingleUseDestroy && callback != null
                        && item.Base is SingleUseKeycardItem singleUseKeycard)
                        singleUseKeycard._destroyed = true;

                    return true;
                }

                return false;
            }
            catch (Exception ex) { Log.Error($"远程钥匙卡-权限检查: {ex.Message}"); return false; }
        }
    }
}
