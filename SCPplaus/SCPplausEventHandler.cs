using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using UnityEngine;

namespace SCPplaus
{
    public class SCPplausEventHandler
    {
        private readonly SCPplaus _plugin;

        /// <summary>SCP 攻击冷却：UserId → 上次攻击时间（用于049攻击间隔等）</summary>
        private readonly Dictionary<string, DateTime> _scpAttackCooldowns = new Dictionary<string, DateTime>();

        public SCPplausEventHandler(SCPplaus plugin)
        {
            _plugin = plugin;
        }

        /// <summary>根据角色类型获取配置血量，0表示不修改</summary>
        private int GetConfigHp(RoleTypeId role)
        {
            return role switch
            {
                RoleTypeId.Scp173 => _plugin.Config.Scp173MaxHp,
                RoleTypeId.Scp049 => _plugin.Config.Scp049MaxHp,
                RoleTypeId.Scp096 => _plugin.Config.Scp096MaxHp,
                RoleTypeId.Scp106 => _plugin.Config.Scp106MaxHp,
                RoleTypeId.Scp939 => _plugin.Config.Scp939MaxHp,
                RoleTypeId.Scp3114 => _plugin.Config.Scp3114MaxHp,
                RoleTypeId.Scp079 => _plugin.Config.Scp079MaxHp,
                RoleTypeId.Scp0492 => _plugin.Config.Scp0492MaxHp,
                _ => 0
            };
        }

        /// <summary>根据角色类型获取配置护盾(AHP)，0表示不设置</summary>
        private float GetConfigShield(RoleTypeId role)
        {
            return role switch
            {
                RoleTypeId.Scp173 => _plugin.Config.Scp173Shield,
                RoleTypeId.Scp096 => _plugin.Config.Scp096Shield,
                RoleTypeId.Scp939 => _plugin.Config.Scp939Shield,
                RoleTypeId.Scp3114 => _plugin.Config.Scp3114Shield,
                _ => 0f
            };
        }

        // ==================== 生成时设置血量 + 护盾 ====================

        /// <summary>核心血量设置：若该角色配置了血量则覆盖 MaxHealth/Health（SCP 专用）</summary>
        /// <param name="resetHealth">true=同时重置 Health 到最大值（用于 OnSpawned/OnChangingRole）；
        /// false=只校正 MaxHealth 不动 Health（用于 OnHurtingEnsureHp 兜底，避免受伤时血量被恢复导致"不掉血"）</param>
        private void ApplyConfigHp(Player player, RoleTypeId role, bool resetHealth = true)
        {
            try
            {
                if (player == null) return;
                int hp = GetConfigHp(role);
                if (hp <= 0) return;
                player.MaxHealth = hp;
                if (resetHealth)
                    player.Health = hp;
                else
                {
                    // 只校正 MaxHealth：若当前 Health 超过旧 MaxHealth 上限，按比例缩放到新 MaxHealth
                    // 避免直接设 Health 把掉血效果"恢复"
                    try
                    {
                        float cur = player.Health;
                        float oldMax = player.MaxHealth;
                        if (oldMax > 0 && cur > hp)
                        {
                            float ratio = hp / oldMax;
                            player.Health = cur * ratio;
                        }
                    }
                    catch { }
                }
                if (_plugin.Config.Debug)
                    Log.Debug($"[SCPplaus] 设置 {role} 血量: {hp} (玩家: {player.Nickname}, resetHealth={resetHealth})");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] 设置血量失败: {ex.Message}");
            }
        }

        /// <summary>设置角色护盾(HS HumeShield)：覆盖原生 HumeShield，不设置 AHP（用户要求护盾用 HS 而非 AHP）</summary>
        private void ApplyConfigShield(Player player, RoleTypeId role)
        {
            try
            {
                if (player == null) return;
                float shield = GetConfigShield(role);
                if (shield <= 0) return;

                // 所有 SCP 都用 HumeShield（HS）作为护盾，不设置 AHP
                SetHumeShield(player, shield);

                if (_plugin.Config.Debug)
                    Log.Debug($"[SCPplaus] 设置 {role} 护盾(HS): {shield} (玩家: {player.Nickname})");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] 设置护盾失败: {ex.Message}");
            }
        }

        /// <summary>设置玩家 HumeShield（HS）护盾：优先直接属性，其次反射找原生组件字段</summary>
        private static void SetHumeShield(Player player, float shield)
        {
            try
            {
                if (player == null || player.ReferenceHub == null) return;

                // 方式1：EXILED Player.HumeShield 属性（SCP-173 等有 HS 的角色）
                var hsProp = player.GetType().GetProperty("HumeShield");
                if (hsProp != null)
                {
                    try { if (hsProp.CanWrite) { hsProp.SetValue(player, shield); return; } } catch { }
                    // 只读时尝试读 MaxHumeShield
                    var maxHsProp = player.GetType().GetProperty("MaxHumeShield");
                    if (maxHsProp != null && maxHsProp.CanWrite)
                        maxHsProp.SetValue(player, shield);
                }

                // 方式2：通过反射找原生 Scp173PlayerScript / 角色脚本中的 HumeShield 字段
                var roleBase = player.Role?.Base;
                if (roleBase != null)
                {
                    // 尝试设置 HumeShield 相关字段（多名字兜底）
                    TrySetHumeField(roleBase, shield);
                    // 遍历角色的所有属性/字段，找含 HumeShield 的对象
                    foreach (var p in roleBase.GetType().GetProperties(
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        try
                        {
                            if (p.Name.IndexOf("Script", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var v = p.GetValue(roleBase);
                                if (v != null) TrySetHumeField(v, shield);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void TrySetHumeField(object obj, float shield)
        {
            try
            {
                if (obj == null) return;
                foreach (var name in new[] { "HumeShield", "Hume", "Shield", "HumeMax", "MaxHumeShield", "CurrentHume", "HumeRegeneration" })
                {
                    var f = obj.GetType().GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(int)))
                    {
                        if (f.FieldType == typeof(float)) f.SetValue(obj, shield);
                        else f.SetValue(obj, (int)Math.Round(shield));
                        return;
                    }
                    var p = obj.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (p != null && p.CanWrite && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
                    {
                        if (p.PropertyType == typeof(float)) p.SetValue(obj, shield);
                        else p.SetValue(obj, (int)Math.Round(shield));
                        return;
                    }
                }
            }
            catch { }
        }

        // ==================== 人类阵营武器强化（7778 加强服） ====================

        /// <summary>
        /// 人类武器强化：
        /// - 混沌掠夺者 + 混沌其他角色 → 大机枪 (GunLogicer)
        /// - 九尾（NTF）全部 → 九尾机枪 (GunE11SR)
        /// - 九尾指挥官（NtfCaptain）→ 额外获得 HID 电炮 (MicroHID)
        /// 仅对非 SCP 角色生效；若已持有同类武器则跳过，避免重复。
        /// </summary>
        private void GiveHumanWeapons(Player player, RoleTypeId role)
        {
            try
            {
                if (player == null || player.IsScp) return;

                // ===== 混沌分裂者 → 大机枪 =====
                if (role == RoleTypeId.ChaosConscript ||
                    role == RoleTypeId.ChaosRifleman ||
                    role == RoleTypeId.ChaosMarauder ||
                    role == RoleTypeId.ChaosRepressor)
                {
                    AddWeaponIfMissing(player, ItemType.GunLogicer, "大机枪");
                }

                // ===== 九尾(NTF) → 九尾机枪 =====
                if (role == RoleTypeId.NtfPrivate ||
                    role == RoleTypeId.NtfSergeant ||
                    role == RoleTypeId.NtfSpecialist ||
                    role == RoleTypeId.NtfCaptain)
                {
                    AddWeaponIfMissing(player, ItemType.GunE11SR, "九尾机枪");
                }

                // ===== 九尾指挥官 → HID 电炮 =====
                if (role == RoleTypeId.NtfCaptain)
                {
                    AddWeaponIfMissing(player, ItemType.MicroHID, "HID电炮");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] GiveHumanWeapons: {ex.Message}");
            }
        }

        /// <summary>若玩家未持有指定武器则添加，避免重复发放</summary>
        private static void AddWeaponIfMissing(Player player, ItemType item, string name)
        {
            try
            {
                if (player == null) return;
                // 检查是否已持有同类武器
                foreach (var it in player.Items)
                {
                    if (it != null && it.Type == item)
                        return; // 已持有，跳过
                }
                player.AddItem(item);
                if (SCPplaus.Instance?.Config?.Debug == true)
                    Log.Debug($"[SCPplaus] 给 {player.Nickname} 发放 {name}");
            }
            catch { }
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                var role = ev.Player.Role.Type;
                ApplyConfigHp(ev.Player, role);
                ApplyConfigShield(ev.Player, role);

                // 人类阵营武器强化（仅对非 SCP 生效）
                GiveHumanWeapons(ev.Player, role);

                // SCP-079 特殊处理：初始化升级系统（重置本局经验 + 应用第1层 AP）
                if (role == RoleTypeId.Scp079)
                    _plugin.Scp079?.On079Spawned(ev.Player);

                // SCP-049-2 特殊处理：给 SCP-049 加经验
                if (role == RoleTypeId.Scp0492)
                {
                    Give049ResurrectExp(ev.Player);

                    // 强制手持 COM-15
                    try
                    {
                        var item = ev.Player.AddItem(ItemType.GunCOM15);
                        if (item != null) ev.Player.CurrentItem = item;
                    }
                    catch (Exception ex)
                    {
                        if (_plugin.Config.Debug) Log.Debug($"[SCPplaus] 049-2 发放 COM-15 失败: {ex.Message}");
                    }

                    // 被小僵尸感染的固定 200 血（覆盖 ApplyConfigHp 的配置值）
                    if (ZombieInfectionManager.IsZombieInfected(ev.Player.UserId))
                    {
                        ev.Player.MaxHealth = 200f;
                        ev.Player.Health = 200f;
                    }

                    // 注册感染技能按键（每个 049-2 一次感染机会）
                    ZombieInfectionManager.RegisterKeys(ev.Player);
                }

                // ===== SCP-049 救人时间设置 =====
                if (role == RoleTypeId.Scp049 && _plugin.Config.Scp049ResurrectTime > 0)
                {
                    try
                    {
                        var roleBase = ev.Player.Role.Base;
                        if (roleBase == null) return;
                        Apply049ResurrectTime(roleBase, _plugin.Config.Scp049ResurrectTime);
                    }
                    catch (Exception ex)
                    {
                        if (_plugin.Config.Debug)
                            Log.Debug($"[SCPplaus] 设置049救人时间失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[SCPplaus] OnSpawned: " + ex.Message);
            }
        }

        /// <summary>
        /// 设置 SCP-049 复活 049-2 的时间。
        /// 原生逻辑：复活时间存在 Scp049ResurrectAbility 组件的 TotalTimeForResurrect 等字段中，
        /// 该组件是 Scp049Role 的属性。需先从 roleBase 找到组件实例，再设置其时间字段。
        /// </summary>
        private void Apply049ResurrectTime(object roleBase, float time)
        {
            try
            {
                if (roleBase == null) return;
                // HashSet<object> 对引用类型默认即引用相等（.NET Framework 4.8 无 ReferenceEqualityComparer）
                var visited = new HashSet<object>();

                // 递归查找含复活时间字段的对象
                void Walk(object obj)
                {
                    if (obj == null || visited.Contains(obj)) return;
                    try { visited.Add(obj); }
                    catch { return; }

                    var type = obj.GetType();
                    // 1. 直接设置含 Resurrect/TotalTime 的字段
                    foreach (var field in type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                    {
                        if ((field.FieldType == typeof(float) || field.FieldType == typeof(int)) &&
                            (field.Name.IndexOf("Resurrect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             field.Name.IndexOf("TotalTime", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            try
                            {
                                if (field.FieldType == typeof(float)) field.SetValue(obj, time);
                                else field.SetValue(obj, (int)Math.Round(time));
                                if (SCPplaus.Instance?.Config?.Debug == true)
                                    Log.Debug($"[SCPplaus] 设置049救人时间字段 {field.Name}={time}");
                            }
                            catch { }
                        }
                    }
                    // 2. 设置含 Resurrect/TotalTime 的属性（float/int）
                    foreach (var prop in type.GetProperties(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                    {
                        if (prop.CanWrite && (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int)) &&
                            (prop.Name.IndexOf("Resurrect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             prop.Name.IndexOf("TotalTime", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            try
                            {
                                if (prop.PropertyType == typeof(float)) prop.SetValue(obj, time);
                                else prop.SetValue(obj, (int)Math.Round(time));
                                if (SCPplaus.Instance?.Config?.Debug == true)
                                    Log.Debug($"[SCPplaus] 设置049救人时间属性 {prop.Name}={time}");
                            }
                            catch { }
                        }
                    }
                    // 3. 递归遍历子对象（找 Scp049ResurrectAbility 等组件）
                    foreach (var prop in type.GetProperties(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                    {
                        if (!prop.CanRead) continue;
                        if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(int) || prop.PropertyType == typeof(string))
                            continue;
                        try
                        {
                            if (prop.Name.IndexOf("Scp049", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                prop.Name.IndexOf("Resurrect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                prop.PropertyType.Name.IndexOf("Ability", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                prop.PropertyType.Name.IndexOf("Scp049", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var child = prop.GetValue(obj);
                                if (child != null) Walk(child);
                            }
                        }
                        catch { }
                    }
                    foreach (var field in type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
                    {
                        if (field.FieldType == typeof(float) || field.FieldType == typeof(int) || field.FieldType == typeof(string))
                            continue;
                        try
                        {
                            if (field.Name.IndexOf("Scp049", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                field.Name.IndexOf("Resurrect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                field.FieldType.Name.IndexOf("Ability", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                field.FieldType.Name.IndexOf("Scp049", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var child = field.GetValue(obj);
                                if (child != null) Walk(child);
                            }
                        }
                        catch { }
                    }
                }

                Walk(roleBase);
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] Apply049ResurrectTime: {ex.Message}");
            }
        }

        // ==================== 中途换角色设置血量（管理员后加的SCP） ====================

        /// <summary>
        /// 角色切换事件：覆盖管理员游戏中途用 setclass/role 等指令把玩家改成 SCP 的情况。
        /// Spawned 事件在部分中途换角色（非死亡重生）时可能不触发，这里做兜底。
        /// ChangingRole 在切换前触发，用 ev.NewRole（目标角色）判断并设置血量。
        /// </summary>
        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                // 目标是 SCP 才需要改血量/护盾；NewRole 为即将切换的角色
                if (!IsScpRole(ev.NewRole)) return;
                ApplyConfigHp(ev.Player, ev.NewRole);
                ApplyConfigShield(ev.Player, ev.NewRole);
                if (_plugin.Config.Debug)
                    Log.Debug($"[SCPplaus] ChangingRole → {ev.NewRole} (玩家: {ev.Player.Nickname})");
            }
            catch (Exception ex)
            {
                Log.Error("[SCPplaus] OnChangingRole: " + ex.Message);
            }
        }

        /// <summary>判断是否为 SCP 角色（非 SCP-049-2 之外的所有 SCP）</summary>
        private static bool IsScpRole(RoleTypeId role)
        {
            return role == RoleTypeId.Scp173 ||
                   role == RoleTypeId.Scp049 ||
                   role == RoleTypeId.Scp096 ||
                   role == RoleTypeId.Scp106 ||
                   role == RoleTypeId.Scp939 ||
                   role == RoleTypeId.Scp079 ||
                   role == RoleTypeId.Scp0492 ||
                   role == RoleTypeId.Scp3114;
        }

        /// <summary>
        /// 受伤时血量兜底校正：若 SCP 当前 MaxHealth 与配置不符（例如血量被覆盖/管理员新加SCP未吃到Spawned），
        /// 在受伤瞬间将其校正为配置值，保证后续血量始终正确。
        /// 注意：只校正 MaxHealth，不重置 Health——避免受伤时血量被恢复导致"不掉血"的 Bug。
        /// </summary>
        public void OnHurtingEnsureHp(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Player == null || !ev.Player.IsScp) return;
                ApplyConfigHp(ev.Player, ev.Player.Role.Type, resetHealth: false);
            }
            catch { }
        }

        // ==================== 受伤前拦截（可修改伤害值） ====================

        public void OnHurting(HurtingEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;

                RoleTypeId attackerRole = ev.Attacker.Role.Type;
                bool victimIsHuman = !ev.Player.IsScp;

                // ===== SCP-049：攻击间隔控制 + 固定伤害 =====
                if (attackerRole == RoleTypeId.Scp049 && victimIsHuman)
                {
                    // 攻击间隔控制（冷却中拦截）
                    if (_plugin.Config.Scp049AttackInterval > 0 && IsAttackOnCooldown(ev.Attacker, _plugin.Config.Scp049AttackInterval))
                    {
                        ev.Amount = 0f; // 冷却中不造成伤害
                        ev.IsAllowed = false; // 拦截本次攻击
                        if (_plugin.Config.Debug)
                            Log.Debug($"[SCPplaus] 049攻击间隔冷却中，已拦截");
                        return;
                    }
                    RecordAttackTime(ev.Attacker);
                    // 固定伤害（若配置>0）
                    if (_plugin.Config.Scp049Damage > 0)
                    {
                        ev.Amount = _plugin.Config.Scp049Damage;
                        if (_plugin.Config.Debug)
                            Log.Debug($"[SCPplaus] 049伤害设置为 {ev.Amount} → {ev.Player.Nickname}");
                    }
                }

                // ===== SCP-939：攻击伤害固定 =====
                // 在 Hurting(受伤前)直接修改 ev.Amount（可写），避免在 Hurt(受伤后)里改(只读)或调用 Hurt() 造成无限递归崩溃
                if (attackerRole == RoleTypeId.Scp939 && victimIsHuman && _plugin.Config.Scp939Damage > 0)
                {
                    ev.Amount = _plugin.Config.Scp939Damage;
                    if (_plugin.Config.Debug)
                        Log.Debug($"[SCPplaus] 939伤害设置为 {ev.Amount} → {ev.Player.Nickname}");
                }

                // ===== SCP-3114：攻击伤害固定 =====
                if (attackerRole == RoleTypeId.Scp3114 && victimIsHuman && _plugin.Config.Scp3114Damage > 0)
                {
                    ev.Amount = _plugin.Config.Scp3114Damage;
                    if (_plugin.Config.Debug)
                        Log.Debug($"[SCPplaus] 3114伤害设置为 {ev.Amount} → {ev.Player.Nickname}");
                }

                // ===== SCP-096：血量越低伤害越高 =====
                // 初始伤害 Scp096BaseDamage，每降低10%血量额外 +Scp096DamagePer10Percent 伤害
                if (attackerRole == RoleTypeId.Scp096 && victimIsHuman)
                {
                    float baseDmg = _plugin.Config.Scp096BaseDamage > 0 ? _plugin.Config.Scp096BaseDamage : 50f;
                    float lostPct = 1f - GetHpRatio(ev.Attacker);
                    int steps = (int)Math.Floor(lostPct * 10f); // 每10%一个档位
                    float total = baseDmg + steps * _plugin.Config.Scp096DamagePer10Percent;
                    ev.Amount = total;
                    if (_plugin.Config.Debug)
                        Log.Debug($"[SCPplaus] 096伤害(失血{lostPct:P0}) = {total} → {ev.Player.Nickname}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] OnHurting: {ex.Message}");
            }
        }

        /// <summary>获取玩家当前血量比例（0~1，存活时0<ratio<=1）</summary>
        private static float GetHpRatio(Player player)
        {
            try
            {
                if (player == null) return 1f;
                float max = player.MaxHealth;
                if (max <= 0) return 1f;
                return Mathf.Clamp01(player.Health / max);
            }
            catch { return 1f; }
        }

        /// <summary>判断该攻击者是否处于攻击冷却中（距离上次攻击不足 interval 秒）</summary>
        private bool IsAttackOnCooldown(Player attacker, float interval)
        {
            string id = attacker?.UserId;
            if (string.IsNullOrEmpty(id)) return false;
            if (_scpAttackCooldowns.TryGetValue(id, out DateTime last))
                return (DateTime.Now - last).TotalSeconds < interval;
            return false;
        }

        /// <summary>记录攻击时间</summary>
        private void RecordAttackTime(Player attacker)
        {
            string id = attacker?.UserId;
            if (string.IsNullOrEmpty(id)) return;
            _scpAttackCooldowns[id] = DateTime.Now;
        }

        // ==================== 伤害处理 ====================

        public void OnHurt(HurtEventArgs ev)
        {
            try
            {
                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;

                RoleTypeId attackerRole = ev.Attacker.Role.Type;
                bool victimIsHuman = !ev.Player.IsScp;
                if (!victimIsHuman) return;

                // ===== SCP-106：75%概率抓入口袋，未抓到掉血35 =====
                if (attackerRole == RoleTypeId.Scp106)
                {
                    float chance = _plugin.Config.Scp106PocketChance;
                    bool grab = chance >= 1f || UnityEngine.Random.value < chance;

                    if (grab)
                    {
                        // 抓入口袋（活着进维度）
                        try
                        {
                            // 授予腐蚀(Corroding)状态：游戏原生机制会把玩家传入口袋维度
                            // （原 Type.GetType("PocketDimensionTeleport, Assembly-CSharp-Publicized") 反射
                            //   在服务器上因程序集名错误恒为 null，且 EXILED 9.13.3 无 SendToPocketDimension API）
                            ev.Player.EnableEffect(EffectType.Corroding);
                            if (_plugin.Config.Debug)
                                Log.Debug($"[SCPplaus] 106抓{ev.Player.Nickname}进口袋");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[SCPplaus] 106入口袋: {ex.Message}");
                        }
                    }
                    else
                    {
                        // 未抓到：额外造成 Scp106PocketMissDamage 伤害
                        if (_plugin.Config.Scp106PocketMissDamage > 0)
                        {
                            try { ev.Player.Hurt(_plugin.Config.Scp106PocketMissDamage, Exiled.API.Enums.DamageType.Scp106); } catch { }
                            if (_plugin.Config.Debug)
                                Log.Debug($"[SCPplaus] 106未抓到，{ev.Player.Nickname} 掉血{_plugin.Config.Scp106PocketMissDamage}");
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[SCPplaus] OnHurt: " + ex.Message);
            }
        }

        // ==================== SCP-079 升级系统事件包装 ====================

        /// <summary>SCP-079 生成时：初始化升级系统（由 OnSpawned 内联调用，此方法保留备用）</summary>
        public void OnSpawnedWithScp079(SpawnedEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                if (ev.Player.Role.Type == RoleTypeId.Scp079)
                    _plugin.Scp079?.On079Spawned(ev.Player);
            }
            catch { }
        }

        /// <summary>玩家死亡：若击杀者是 SCP-079 则累积升级经验</summary>
        public void OnDiedWithScp079Exp(DiedEventArgs ev)
        {
            try
            {
                // 玩家死亡：注销 049-2 感染按键
                ZombieInfectionManager.UnregisterKeys(ev.Player);

                if (ev.Attacker == null || ev.Player == null) return;
                if (ev.Attacker == ev.Player) return;
                if (ev.Attacker.Role.Type != RoleTypeId.Scp079) return;
                if (ev.Player.IsScp) return;
                _plugin.Scp079?.On079Kill(ev.Attacker, ev.Player);
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] OnDiedWithScp079Exp: {ex.Message}");
            }
        }

        /// <summary>回合结束：清空 SCP-079 升级经验 + 感染状态</summary>
        public void OnRoundEndedWithScp079(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            try
            {
                ZombieInfectionManager.Reset();
                _plugin.Scp079?.OnRoundEnd();
            }
            catch { }
        }

        /// <summary>049-2 生成时检测附近SCP-049并加经验</summary>
        private void Give049ResurrectExp(Player zombiePlayer)
        {
            try
            {
                if (zombiePlayer == null) return;

                // 查找附近的 SCP-049
                var scp049 = Player.List.FirstOrDefault(p =>
                    p.IsAlive && p.Role.Type == RoleTypeId.Scp049 &&
                    Vector3.Distance(p.Position, zombiePlayer.Position) < 20f);

                if (scp049 != null)
                    AddExpTo049(scp049);
            }
            catch { }
        }

        /// <summary>给SCP-049添加150经验（通过反射调用 ExperiencePlugin）</summary>
        private static void AddExpTo049(Player scp049)
        {
            try
            {
                if (scp049 == null) return;

                // 通过反射获取 ExperiencePlugin 实例
                var expAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ExperiencePlugin");
                if (expAsm == null) return;

                var expType = expAsm.GetType("ExperiencePlugin.ExperiencePlugin");
                if (expType == null) return;

                var instanceProp = expType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                var expInstance = instanceProp?.GetValue(null);
                if (expInstance == null) return;

                var dataMgrProp = expType.GetProperty("DataManager",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var dataMgr = dataMgrProp?.GetValue(expInstance);
                if (dataMgr == null) return;

                // 调用 AddExperience 方法
                var addExpMethod = dataMgr.GetType().GetMethod("AddExperience",
                    new[] { typeof(Player), typeof(int) });
                if (addExpMethod != null)
                {
                    addExpMethod.Invoke(dataMgr, new object[] { scp049, 150 });
                    Log.Info("[SCPplaus] SCP-049 " + scp049.Nickname + " 复活尸体 +150经验");
                }

                // 显示提示
                scp049.ShowHint("<size=14><color=#44FF88>复活成功 +150经验</color></size>", 3f);
            }
            catch (Exception ex)
            {
                Log.Warn("[SCPplaus] 给049加经验失败: " + ex.Message);
            }
        }

        private static float GetDamageHandlerDamage(object handler)
        {
            try
            {
                // 尝试 Damage 属性
                var damageProp = handler.GetType().GetProperty("Damage",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (damageProp != null)
                    return Convert.ToSingle(damageProp.GetValue(handler));

                // 尝试 BaseDamage 属性
                var baseProp = handler.GetType().GetProperty("BaseDamage",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (baseProp != null)
                    return Convert.ToSingle(baseProp.GetValue(handler));

                // 尝试 _damage 字段
                var field = handler.GetType().GetField("_damage",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return Convert.ToSingle(field.GetValue(handler));
            }
            catch { }
            return 0;
        }
    }
}
