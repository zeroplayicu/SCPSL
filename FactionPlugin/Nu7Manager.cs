using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// NU7-A 阵营（2026-10-06 新增）。
    /// 载体同 GOC：Tutorial 角色（原生武器伤害对多数目标无效 → 伤害由 GocGunFireWatcher 统一按兵种表施加）。
    /// 编制（每组 7 人，最多 30 人）：
    ///   1x 医疗兵   —— SCP-500 x4 + 特种E11(35/对SCP 55) + 重甲；技能1：脚底生成 4止痛药+4血包+4肾上腺素+2 SCP-500
    ///   2x 士兵     —— 特种E11(35/55) + 重甲 + 1血包
    ///   1x 狙击手   —— 特种狙击E11(10发弹匣, 450伤害) + 重甲 + 1血包
    ///   1x 指挥官燕双鹰 —— COM-18(30发, 55/对SCP 150) + 移速85% + 重甲 + 1血包
    ///   1x 机枪手   —— 混沌机枪(200发, 45伤害) + 重甲 + 1血包
    ///   1x 先锋     —— P90冲锋枪(50发, 40伤害) + 移速70% + 重甲 + 1血包
    /// </summary>
    public enum Nu7RoleType
    {
        Medic,      // 医疗兵
        Soldier,    // 士兵
        Sniper,     // 狙击手
        Commander,  // 指挥官燕双鹰
        Gunner,     // 机枪手
        Vanguard,   // 先锋
    }

    public static class Nu7Manager
    {
        public static readonly Dictionary<string, Nu7RoleType> Members = new Dictionary<string, Nu7RoleType>();

        private static readonly DateTime _unused = DateTime.Now;
        private static readonly Dictionary<string, DateTime> SkillCooldowns = new Dictionary<string, DateTime>();

        public const int MaxSquad = 30;
        public const float MedicSkillCd = 180f;

        /// <summary>每次刷新的兵种顺序（一组 7 人，循环到上限）</summary>
        private static readonly Nu7RoleType[] SpawnOrder =
        {
            Nu7RoleType.Medic,
            Nu7RoleType.Soldier,
            Nu7RoleType.Soldier,
            Nu7RoleType.Sniper,
            Nu7RoleType.Commander,
            Nu7RoleType.Gunner,
            Nu7RoleType.Vanguard,
        };

        public static bool IsNu7(Player player) => player != null && Members.ContainsKey(player.UserId);

        public static string GetRoleName(Nu7RoleType t)
        {
            switch (t)
            {
                case Nu7RoleType.Medic: return "医疗兵";
                case Nu7RoleType.Soldier: return "士兵";
                case Nu7RoleType.Sniper: return "狙击手";
                case Nu7RoleType.Commander: return "指挥官燕双鹰";
                case Nu7RoleType.Gunner: return "机枪手";
                case Nu7RoleType.Vanguard: return "先锋";
                default: return t.ToString();
            }
        }

        // ===== 刷新 =====

        /// <summary>强制刷新一整队（最多 30 人）</summary>
        public static bool ForceSpawn(out string response)
        {
            response = "";
            try
            {
                var candidates = Player.List
                    .Where(p => p != null && p.IsConnected && p.IsAlive
                                && p.Role.Type != RoleTypeId.Tutorial
                                && !p.IsScp
                                && !GocManager.IsGoc(p))
                    .ToList();

                if (candidates.Count == 0) { response = "没有可刷新的玩家"; return false; }

                int count = Math.Min(MaxSquad, candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    SpawnMember(candidates[i], SpawnOrder[i % SpawnOrder.Length]);
                }
                response = $"NU7-A 已刷新 {count} 人";
                Log.Info($"[NU7-A] 已刷新 {count} 人");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[NU7-A] 刷新失败: {ex.Message}");
                response = "刷新失败: " + ex.Message;
                return false;
            }
        }

        /// <summary>刷新指定兵种（调试用）</summary>
        public static bool ForceSpawnSingle(Nu7RoleType role, out string response)
        {
            response = "";
            try
            {
                var p = Player.List.FirstOrDefault(x => x != null && x.IsConnected && x.IsAlive && x.Role.Type != RoleTypeId.Tutorial && !x.IsScp);
                if (p == null) { response = "没有可用玩家"; return false; }
                SpawnMember(p, role);
                response = $"{p.Nickname} → NU7-A {GetRoleName(role)}";
                return true;
            }
            catch (Exception ex) { response = "失败: " + ex.Message; return false; }
        }

        public static void SpawnMember(Player player, Nu7RoleType role)
        {
            try
            {
                Members[player.UserId] = role;

                // Tutorial 载体（同 GOC）
                player.Role.Set(RoleTypeId.Tutorial);
                player.Health = 150f;
                player.RankName = null;
                player.CustomInfo = null;

                ApplyLoadout(player, role);

                player.ShowHint($"<color=#FFD700>[NU7-A]</color> 你是 <color=#FFFFFF>{GetRoleName(role)}</color>", 6f);
                Log.Info($"[NU7-A] {player.Nickname} → {GetRoleName(role)}");
            }
            catch (Exception ex) { Log.Error($"[NU7-A] 变身失败: {ex.Message}"); }
        }

        // ===== 装备 =====

        private static void ApplyLoadout(Player player, Nu7RoleType role)
        {
            try
            {
                switch (role)
                {
                    case Nu7RoleType.Medic:
                        for (int i = 0; i < 4; i++) player.AddItem(ItemType.SCP500);
                        AddE11(player);
                        player.AddItem(ItemType.Ammo556x45);
                        player.AddItem(ItemType.Ammo556x45);
                        player.AddItem(ItemType.KeycardMTFOperative);
                        break;

                    case Nu7RoleType.Soldier:
                        AddE11(player);
                        player.AddItem(ItemType.Ammo556x45);
                        player.AddItem(ItemType.Ammo556x45);
                        player.AddItem(ItemType.Medkit);
                        player.AddItem(ItemType.KeycardMTFPrivate);
                        break;

                    case Nu7RoleType.Sniper:
                        var sniper = AddE11(player);
                        SetMagazine(sniper, 10);
                        player.AddItem(ItemType.Ammo556x45);
                        player.AddItem(ItemType.Ammo556x45);
                        player.AddItem(ItemType.Medkit);
                        player.AddItem(ItemType.KeycardMTFOperative);
                        break;

                    case Nu7RoleType.Commander:
                        var com18 = player.AddItem(ItemType.GunCOM18);
                        SetMagazine(com18, 30);
                        player.AddItem(ItemType.Ammo9x19);
                        player.AddItem(ItemType.Ammo9x19);
                        player.AddItem(ItemType.Medkit);
                        player.AddItem(ItemType.KeycardO5);
                        EnableInfEffect(player, EffectType.Slowness, 15);   // 移速 85%
                        break;

                    case Nu7RoleType.Gunner:
                        var mg = player.AddItem(ItemType.GunFRMG0);
                        SetMagazine(mg, 200);
                        player.AddItem(ItemType.Ammo762x39);
                        player.AddItem(ItemType.Ammo762x39);
                        player.AddItem(ItemType.Medkit);
                        player.AddItem(ItemType.KeycardMTFOperative);
                        break;

                    case Nu7RoleType.Vanguard:
                        var smg = player.AddItem(ItemType.GunCrossvec);
                        SetMagazine(smg, 50);
                        player.AddItem(ItemType.Ammo9x19);
                        player.AddItem(ItemType.Ammo9x19);
                        player.AddItem(ItemType.Medkit);
                        player.AddItem(ItemType.KeycardMTFPrivate);
                        EnableInfEffect(player, EffectType.Slowness, 30);   // 移速 70%
                        break;
                }

                // 全员重甲
                try { player.AddItem(ItemType.ArmorHeavy); } catch { }
            }
            catch (Exception ex) { Log.Error($"[NU7-A] 发放装备失败: {ex.Message}"); }
        }

        private static Item AddE11(Player player) => player.AddItem(ItemType.GunE11SR);

        /// <summary>设置弹匣容量（⚠️ 只对普通弹匣武器调用，左轮等弹巢类禁止）</summary>
        private static void SetMagazine(Item item, int ammo)
        {
            try
            {
                var fa = item?.As<Firearm>();
                if (fa != null)
                {
                    fa.MaxMagazineAmmo = (byte)ammo;
                    fa.MagazineAmmo = (byte)ammo;
                }
            }
            catch (Exception ex) { Log.Debug($"[NU7-A] 弹匣设置失败: {ex.Message}"); }
        }

        private static void EnableInfEffect(Player player, EffectType type, byte intensity)
        {
            try { player.EnableEffect(type, intensity, 99999f); } catch { }
        }

        // ===== 医疗兵技能1：脚底生成补给 =====

        public static void TryMedicSkill(Player player)
        {
            if (player == null) return;
            if (!Members.TryGetValue(player.UserId, out var role) || role != Nu7RoleType.Medic) return;

            string key = player.UserId;
            if (SkillCooldowns.TryGetValue(key, out DateTime ready) && DateTime.Now < ready)
            {
                player.ShowHint($"<color=#FF4444>[NU7-A] 补给冷却中 {Math.Max(0, (ready - DateTime.Now).TotalSeconds):0}s</color>", 2f);
                return;
            }
            SkillCooldowns[key] = DateTime.Now.AddSeconds(MedicSkillCd);

            try
            {
                Vector3 pos = player.Position;
                for (int i = 0; i < 4; i++) SpawnPickup(ItemType.Painkillers, pos);
                for (int i = 0; i < 4; i++) SpawnPickup(ItemType.Medkit, pos);
                for (int i = 0; i < 4; i++) SpawnPickup(ItemType.Adrenaline, pos);
                for (int i = 0; i < 2; i++) SpawnPickup(ItemType.SCP500, pos);

                player.ShowHint("<color=#44FF88>[NU7-A 医疗兵]</color> 已投放补给：止痛药x4 血包x4 肾上腺素x4 SCP-500x2", 5f);
                Map.Broadcast(4, $"<color=#44FF88>[NU7-A]</color> {player.Nickname} 投放了一批补给！");
                Log.Info($"[NU7-A] 医疗兵 {player.Nickname} 释放补给");
            }
            catch (Exception ex) { Log.Error($"[NU7-A] 补给失败: {ex.Message}"); }
        }

        private static void SpawnPickup(ItemType type, Vector3 basePos)
        {
            try
            {
                var rnd = new Vector3(
                    (float)(UnityEngine.Random.value * 2.0 - 1.0) * 1.2f, 0.3f,
                    (float)(UnityEngine.Random.value * 2.0 - 1.0) * 1.2f);
                var pk = Exiled.API.Features.Pickups.Pickup.Create(type);
                if (pk != null) pk.Position = basePos + rnd;
            }
            catch { }
        }

        // ===== 清理 =====

        public static void OnRoundEnded()
        {
            var ids = Members.Keys.ToList();
            foreach (var id in ids)
            {
                var p = Player.Get(id);
                if (p != null)
                {
                    try { p.DisableAllEffects(); } catch { }
                }
            }
            Members.Clear();
            SkillCooldowns.Clear();
        }

        public static void ClearMember(Player player)
        {
            if (player == null) return;
            Members.Remove(player.UserId);
            SkillCooldowns.Remove(player.UserId);
        }

        // ===== 伤害表（供 GocGunFireWatcher 查询）=====

        /// <summary>取 NU7 兵种的武器伤害（0 = 无规则）</summary>
        public static float GetWeaponDamage(Nu7RoleType role, ItemType weapon, bool targetIsScp)
        {
            switch (role)
            {
                case Nu7RoleType.Medic:
                case Nu7RoleType.Soldier:
                    if (weapon == ItemType.GunE11SR) return targetIsScp ? 55f : 35f;
                    break;
                case Nu7RoleType.Sniper:
                    if (weapon == ItemType.GunE11SR) return 450f;      // 特种狙击枪
                    break;
                case Nu7RoleType.Commander:
                    if (weapon == ItemType.GunCOM18) return targetIsScp ? 150f : 55f;
                    break;
                case Nu7RoleType.Gunner:
                    if (weapon == ItemType.GunFRMG0) return 45f;
                    break;
                case Nu7RoleType.Vanguard:
                    if (weapon == ItemType.GunCrossvec) return 40f;     // P90 冲锋枪
                    break;
            }
            return 0f;
        }

        /// <summary>HUD 介绍文本</summary>
        public static string GetIntro(Player player)
        {
            if (player == null || !Members.TryGetValue(player.UserId, out var role)) return null;
            switch (role)
            {
                case Nu7RoleType.Medic:
                    return "<color=#4AA5FF>[NU7-A 医疗兵]</color> <color=#AAAAAA>特种E11(35/对SCP 55) + 重甲 + SCP-500x4</color> <color=#FFD700>[技能1]</color> <color=#FFFFFF>投放补给</color> <color=#AAAAAA>脚底生成 止痛药x4 血包x4 肾上腺素x4 SCP-500x2（CD180秒）</color>";
                case Nu7RoleType.Soldier:
                    return "<color=#4AA5FF>[NU7-A 士兵]</color> <color=#AAAAAA>特种E11(35/对SCP 55) + 重甲 + 血包x1</color>";
                case Nu7RoleType.Sniper:
                    return "<color=#4AA5FF>[NU7-A 狙击手]</color> <color=#AAAAAA>特种狙击枪(450伤害/10发) + 重甲 + 血包x1</color>";
                case Nu7RoleType.Commander:
                    return "<color=#4AA5FF>[NU7-A 指挥官燕双鹰]</color> <color=#AAAAAA>COM-18(55/对SCP 150/30发) + 移速85% + 重甲 + 血包x1</color>";
                case Nu7RoleType.Gunner:
                    return "<color=#4AA5FF>[NU7-A 机枪手]</color> <color=#AAAAAA>混沌机枪(45伤害/200发) + 重甲 + 血包x1</color>";
                case Nu7RoleType.Vanguard:
                    return "<color=#4AA5FF>[NU7-A 先锋]</color> <color=#AAAAAA>P90冲锋枪(40伤害/50发) + 移速70% + 重甲 + 血包x1</color>";
            }
            return null;
        }
    }
}
