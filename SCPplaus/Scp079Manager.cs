using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using PlayerRoles;
using UnityEngine;

namespace SCPplaus
{
    /// <summary>
    /// SCP-079 分层升级系统：
    /// - SCP-079 每局内通过击杀人类获得专属经验
    /// - 经验达到各层阈值自动升级
    /// - 每层不同：最大 AP + AP 再生速度（只改 AP 数值，不拦截原生能力）
    /// </summary>
    public class Scp079Manager
    {
        private readonly SCPplaus _plugin;

        // SCP-079 玩家 UserId → 当前局内累积经验
        private readonly Dictionary<string, int> _scp079Exp = new Dictionary<string, int>();

        // 各层解锁所需累计经验（阈值）
        private readonly int[] _levelThresholds = { 0, 80, 210, 460, 960 };

        // 各层最大 AP（索引0=第1层 ... 4=第5层）
        private readonly float[] _maxAp = { 100f, 110f, 125f, 150f, 200f };

        // 各层 AP 再生速度（每秒）
        private readonly float[] _apRegen = { 2f, 2.5f, 4.5f, 5.1f, 7f };

        public Scp079Manager(SCPplaus plugin)
        {
            _plugin = plugin;
        }

        /// <summary>获取玩家的当前等级（1~5）</summary>
        public int GetLevel(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return 1;
            int exp = _scp079Exp.TryGetValue(userId, out int e) ? e : 0;
            for (int i = _levelThresholds.Length - 1; i >= 0; i--)
            {
                if (exp >= _levelThresholds[i])
                    return i + 1;
            }
            return 1;
        }

        /// <summary>获取当前等级(索引0-4)对应的配置</summary>
        private void GetLevelConfig(int level, out float maxAp, out float regen)
        {
            int idx = Mathf.Clamp(level - 1, 0, _maxAp.Length - 1);
            maxAp = _maxAp[idx];
            regen = _apRegen[idx];
        }

        /// <summary>SCP-079 生成时调用：重置本局经验并应用第1层 AP</summary>
        public void On079Spawned(Player player)
        {
            try
            {
                if (player == null) return;
                _scp079Exp[player.UserId] = 0;
                ApplyApConfig(player, 1);
                if (_plugin.Config.Debug)
                    Log.Debug($"[SCP079] {player.Nickname} 生成，初始第1层 AP");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP079] On079Spawned: {ex.Message}");
            }
        }

        /// <summary>SCP-079 击杀/伤害人类时获得经验</summary>
        public void On079Kill(Player killer, Player victim)
        {
            try
            {
                if (killer == null || !killer.IsScp || killer.Role.Type != RoleTypeId.Scp079) return;
                if (victim == null || victim.IsScp) return;

                int oldLevel = GetLevel(killer.UserId);
                int gained = 10; // 每次击杀获得10经验
                if (!_scp079Exp.ContainsKey(killer.UserId))
                    _scp079Exp[killer.UserId] = 0;
                _scp079Exp[killer.UserId] += gained;

                int newLevel = GetLevel(killer.UserId);
                if (newLevel > oldLevel)
                {
                    // 升级！应用新等级 AP
                    ApplyApConfig(killer, newLevel);
                    killer.ShowHint($"<color=#00FFFF>SCP-079 升级到 第{newLevel}层！</color>", 3f);
                    if (_plugin.Config.Debug)
                        Log.Debug($"[SCP079] {killer.Nickname} 升级到第{newLevel}层 (经验 {_scp079Exp[killer.UserId]})");
                }
                else
                {
                    ApplyApConfig(killer, newLevel);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP079] On079Kill: {ex.Message}");
            }
        }

        /// <summary>定时刷新所有在线 SCP-079 的 AP 配置（防止原生逻辑覆盖）</summary>
        public void RefreshAllScp079()
        {
            try
            {
                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && p.IsScp && p.Role.Type == RoleTypeId.Scp079))
                {
                    int level = GetLevel(player.UserId);
                    ApplyApConfig(player, level);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP079] RefreshAllScp079: {ex.Message}");
            }
        }

        /// <summary>应用指定等级的 AP 配置（最大AP + 再生速度）到玩家</summary>
        public void ApplyApConfig(Player player, int level)
        {
            try
            {
                if (player == null || !player.IsScp || player.Role.Type != RoleTypeId.Scp079) return;

                GetLevelConfig(level, out float maxAp, out float regen);

                // 通过反射访问原生 Scp079PlayerScript 的 MaxAP / ApRegen
                var role = player.Role.Base;
                if (role == null) return;

                var script = FindScp079Script(role);
                if (script == null) return;

                // 设置最大 AP（尝试多种字段名）
                foreach (var name in new[] { "MaxAP", "MaxAp", "maxAP", "MaxAHP", "maxAp" })
                    TrySetFieldOrProp(script, name, maxAp);
                // 设置 AP 再生速度
                foreach (var name in new[] { "ApRegen", "apRegen", "APRegen", "RegenerationRate", "regenerationRate", "ApRegeneration", "EnergyRegen" })
                    TrySetFieldOrProp(script, name, regen);
                // 让当前 AP 不超过新的最大值
                float cur = GetApValue(script);
                if (cur >= 0)
                {
                    float capped = Mathf.Min(cur, maxAp);
                    foreach (var name in new[] { "CurrentAp", "currentAP", "CurrentAP", "AP", "Energy" })
                        TrySetFieldOrProp(script, name, capped);
                }

                if (_plugin.Config.Debug)
                    Log.Debug($"[SCP079] {player.Nickname} 第{level}层 AP: 最大{maxAp}, 再生{regen}/s");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP079] ApplyApConfig: {ex.Message}");
            }
        }

        private static object FindScp079Script(object roleBase)
        {
            try
            {
                // 方法1: roleBase 本身就是 Scp079 脚本（含 MaxAP/AP 相关字段）
                if (GetApValue(roleBase) >= 0 || HasField(roleBase, "MaxAP") || HasField(roleBase, "MaxAp"))
                    return roleBase;

                // 方法2: roleBase 含 Scp079PlayerScript 属性
                foreach (var p in roleBase.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (p.Name.IndexOf("Script", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        p.PropertyType.Name.IndexOf("Scp079", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var v = p.GetValue(roleBase);
                        if (v != null && (GetApValue(v) >= 0 || HasField(v, "MaxAP") || HasField(v, "MaxAp")))
                            return v;
                    }
                }

                // 方法3: roleBase 含字段属性指向脚本
                foreach (var f in roleBase.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var v = f.GetValue(roleBase);
                    if (v != null && (GetApValue(v) >= 0 || HasField(v, "MaxAP") || HasField(v, "MaxAp")))
                        return v;
                }
            }
            catch { }
            return null;
        }

        private static bool HasField(object obj, string name)
        {
            try
            {
                if (obj == null) return false;
                return obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null ||
                       obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;
            }
            catch { return false; }
        }

        private static float GetApValue(object obj)
        {
            try
            {
                if (obj == null) return -1;
                foreach (var name in new[] { "CurrentAp", "currentAP", "CurrentAP", "AP", "Energy" })
                {
                    var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(int)))
                        return Convert.ToSingle(f.GetValue(obj));
                    var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)) && p.CanRead)
                        return Convert.ToSingle(p.GetValue(obj));
                }
            }
            catch { }
            return -1;
        }

        private static void TrySetFieldOrProp(object obj, string name, float value)
        {
            try
            {
                if (obj == null) return;
                var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(int)))
                {
                    if (f.FieldType == typeof(float)) f.SetValue(obj, value);
                    else f.SetValue(obj, (int)Math.Round(value));
                    return;
                }
                var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanWrite && (p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
                {
                    if (p.PropertyType == typeof(float)) p.SetValue(obj, value);
                    else p.SetValue(obj, (int)Math.Round(value));
                }
            }
            catch { }
        }

        /// <summary>回合结束清空所有 SCP-079 经验</summary>
        public void OnRoundEnd()
        {
            _scp079Exp.Clear();
        }
    }
}
