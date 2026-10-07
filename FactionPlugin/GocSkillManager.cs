using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// GOC 技能系统：统一两个按键（技能1 / 技能2），回调内按玩家当前兵种分发。
    /// 各兵种技能：
    ///   战斗专家 技能1 - 爆发：加速50% + 强制手持 SCP-1509(伤害180) + 每秒回5HP共25s，CD 120s
    ///   医疗兵   技能1 - 范围治疗：周围队友每秒回8HP共15s，CD 30s
    ///   医疗兵   技能2 - 肾上腺素：全体GOC +50AHP +30%移速共10s，CD 45s
    ///   指挥官   技能1 - 隐身：隐身15s，CD 60s
    ///   指挥官   技能2 - 激励：全体GOC +35%移速 +100AHP +肾上腺素共15s，CD 120s
    /// </summary>
    public static class GocSkillManager
    {
        /// <summary>冷却记录：key = "userId|skillIndex" → 可再次使用的时间</summary>
        private static readonly Dictionary<string, DateTime> Cooldowns = new Dictionary<string, DateTime>();

        /// <summary>战斗专家「抗性」技能持续到该时刻（2026-10-02）</summary>
        private static readonly Dictionary<string, DateTime> BreacherGuardUntil = new Dictionary<string, DateTime>();

        /// <summary>战斗专家是否处于「抗性」技能状态（伤害减免 75%）</summary>
        public static bool IsBreacherGuarding(Player player)
        {
            if (player == null) return false;
            if (!BreacherGuardUntil.TryGetValue(player.UserId, out DateTime until)) return false;
            return DateTime.Now < until;
        }

        /// <summary>技能参数（可按需调整）</summary>
        private const float MedicAreaRadius = 10f;

        public static void Reset()
        {
            Cooldowns.Clear();
            BreacherGuardUntil.Clear();
        }

        // ===== 奇术师特制 A7（技能键触发，因为 Tutorial 角色不触发原生 Shooting 事件）=====

        /// <summary>奇术师 A7：子弹=当前护盾（1HS=1发），无瞄准全向命中 40m 内最近敌人，伤害 30</summary>
        // ===== 奇术师 A7：直接开火（鼠标左键），弹匣轮询检测 =====
        // GOC 载体是 Tutorial 角色，不触发原生 Shooting 事件 → 通过监视 A7 弹匣变化判定开火。

        /// <summary>上次记录的 A7 弹匣余弹（按玩家）</summary>
        private static readonly Dictionary<string, int> A7LastAmmo = new Dictionary<string, int>();

        /// <summary>
        /// 奇术师 A7 开火检测（0.1 秒轮询，由 FactionPlugin.HumeShieldRoutine 调用）。
        /// 玩家直接按鼠标左键开火（原生动画/音效/弹道），每发消耗 1 HS 并全向命中 40m 内最近敌人（30 伤害）；
        /// 弹匣自动补满 → 子弹无限；护盾为 0 时不再补弹 → 枪自然打空（无法射出）。
        /// </summary>
        public static void TickThaumaturgeFire()
        {
            try
            {
                if (GocManager.Members.Count == 0) return;

                foreach (var kv in GocManager.Members.ToList())
                {
                    if (kv.Value != GocRoleType.Thaumaturge) continue;

                    var p = Player.Get(kv.Key);
                    if (p == null || !p.IsConnected || !p.IsAlive)
                    {
                        A7LastAmmo.Remove(kv.Key);
                        continue;
                    }

                    // 必须手持 A7（奇术师的常驻武器）
                    var item = p.CurrentItem;
                    if (item == null || item.Type != ItemType.GunA7)
                    {
                        A7LastAmmo.Remove(kv.Key);
                        continue;
                    }

                    Exiled.API.Features.Items.Firearm fa = null;
                    try { fa = item.As<Exiled.API.Features.Items.Firearm>(); } catch { }
                    if (fa == null) { A7LastAmmo.Remove(kv.Key); continue; }

                    int cur = fa.MagazineAmmo;
                    if (!A7LastAmmo.TryGetValue(kv.Key, out int last))
                    {
                        A7LastAmmo[kv.Key] = cur;
                        continue;
                    }

                    if (cur < last)
                    {
                        // 弹匣减少 = 玩家开火了
                        int shots = last - cur;
                        int fired = 0;
                        for (int i = 0; i < shots; i++)
                        {
                            if (p.HumeShield < 1f) break;      // 护盾不足：本发不生效
                            p.HumeShield = Math.Max(0f, p.HumeShield - 1f);
                            FireOneA7Shot(p);
                            fired++;
                        }

                        if (fired > 0)
                        {
                            // 子弹无限：把弹匣恢复到减少前的数量
                            try { fa.MagazineAmmo = last; } catch { }
                            A7LastAmmo[kv.Key] = fa.MagazineAmmo;
                        }
                        else
                        {
                            // 护盾为空：不补弹 → 枪继续打空
                            A7LastAmmo[kv.Key] = fa.MagazineAmmo;
                            p.ShowHint("<color=#B388FF>[奇术A7]</color> <color=#FF6666>护盾不足，无法射出子弹（静止回盾中）</color>", 1.5f);
                        }
                    }
                    else
                    {
                        A7LastAmmo[kv.Key] = cur;
                    }
                }
            }
            catch (Exception ex) { Log.Debug($"[GOC] A7 开火检测失败: {ex.Message}"); }
        }

        /// <summary>单发命中：40m 内最近敌人，30 伤害（走 Hurting 事件）</summary>
        private static void FireOneA7Shot(Player player)
        {
            try
            {
                Player target = null;
                float best = float.MaxValue;
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected || !p.IsAlive || p == player) continue;
                    if (GocManager.IsGoc(p)) continue;
                    float d = Vector3.Distance(player.Position, p.Position);
                    if (d <= 40f && d < best) { best = d; target = p; }
                }
                if (target == null) return;

                target.Hurt(player, 30f, Exiled.API.Enums.DamageType.Firearm);
                target.ShowHint("<color=#B388FF>[奇术A7]</color> <color=#FF6666>被神秘弹道击中 -30</color>", 1.2f);
            }
            catch { }
        }

        // ===== 冷却 =====

        public static float GetCooldownRemaining(Player player, int skillIndex)
        {
            if (player == null) return 0f;
            if (!Cooldowns.TryGetValue($"{player.UserId}|{skillIndex}", out DateTime readyAt)) return 0f;
            float remain = (float)(readyAt - DateTime.Now).TotalSeconds;
            return remain > 0f ? remain : 0f;
        }

        private static bool CheckCooldown(Player player, int skillIndex, float cdSeconds, string skillName)
        {
            float remain = GetCooldownRemaining(player, skillIndex);
            if (remain > 0f)
            {
                player.ShowHint($"<color=#FF4444>[GOC] {skillName} 冷却中 {remain:0}s</color>", 1.5f);
                return false;
            }
            Cooldowns[$"{player.UserId}|{skillIndex}"] = DateTime.Now.AddSeconds(cdSeconds);
            return true;
        }

        private static void ShowSkillUsed(Player player, string skillName, float cdSeconds)
        {
            player.ShowHint($"<color=#00AEEF>[GOC]</color> <color=#FFD700>{skillName}</color> 已释放 <color=#AAAAAA>(冷却 {cdSeconds:0}s)</color>", 2.5f);
        }

        // ===== 入口（按键绑定回调）=====

        /// <summary>技能 1（按兵种分发）</summary>
        public static void TriggerSkill1(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected || !player.IsAlive) return;
                if (!GocManager.Members.TryGetValue(player.UserId, out GocRoleType role))
                {
                    player.ShowHint("<color=#FF4444>你不是 GOC 成员</color>", 1.5f);
                    return;
                }

                switch (role)
                {
                    case GocRoleType.Breacher: BreacherResist(player); break;
                    case GocRoleType.Vanguard: VanguardOverload(player); break;
                    case GocRoleType.SpecialOps: SpecialOpsRush(player); break;
                    case GocRoleType.Medic: MedicAreaHeal(player); break;
                    case GocRoleType.Commander: CommanderInvisible(player); break;
                    case GocRoleType.Thaumaturge: ThaumaturgeTrap(player); break;
                    default:
                        player.ShowHint("<color=#FF4444>[GOC] 你的兵种没有技能 1</color>", 1.5f);
                        break;
                }
            }
            catch (Exception ex) { Log.Error($"[GOC] 技能1 执行失败: {ex.Message}"); }
        }

        /// <summary>技能 2（按兵种分发）</summary>
        public static void TriggerSkill2(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected || !player.IsAlive) return;
                if (!GocManager.Members.TryGetValue(player.UserId, out GocRoleType role))
                {
                    player.ShowHint("<color=#FF4444>你不是 GOC 成员</color>", 1.5f);
                    return;
                }

                switch (role)
                {
                    case GocRoleType.Medic: MedicAdrenaline(player); break;
                    case GocRoleType.Commander: CommanderInspire(player); break;
                    case GocRoleType.Thaumaturge: ThaumaturgeShock(player); break;
                    case GocRoleType.SpecialOps: SpecialOpsRevolver(player); break;
                    default:
                        player.ShowHint("<color=#FF4444>[GOC] 你的兵种没有技能 2</color>", 1.5f);
                        break;
                }
            }
            catch (Exception ex) { Log.Error($"[GOC] 技能2 执行失败: {ex.Message}"); }
        }

        // ===== 先锋：超负荷（移速 250%，120 秒后自爆死亡）=====

        private static void VanguardOverload(Player player)
        {
            // 一次性技能：冷却设为极大值，触发后不再可用
            if (!CheckCooldown(player, 1, 99999f, "超负荷")) return;

            // 移除基础减速，并把移速拉到 250%（+150%）
            try { player.DisableEffect(EffectType.Slowness); } catch { }
            try { player.EnableEffect(EffectType.MovementBoost, 150, 120f); } catch { }

            player.ShowHint("<color=#FF6600>[超负荷]</color> 移速提升至 <color=#FFFF00>250%</color>！<color=#FF4444>120 秒后自爆死亡</color>", 6f);
            Map.Broadcast(5, $"<color=#FF6600>[GOC]</color> 先锋 <color=#FFFF00>{player.Nickname}</color> 启动超负荷，移速飙升！");
            Log.Info($"[GOC] 先锋 {player.Nickname} 启动超负荷（120 秒后自爆）");

            // 120 秒后自爆死亡（若期间已不是 GOC 先锋/已死亡则跳过）
            Timing.CallDelayed(120f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected || !player.IsAlive) return;
                    if (!GocManager.Members.TryGetValue(player.UserId, out GocRoleType nowRole) || nowRole != GocRoleType.Vanguard) return;
                    player.Kill("超负荷自爆");
                    Map.Broadcast(4, "<color=#FF6600>[GOC]</color> 先锋的超负荷到达极限，自爆身亡！");
                }
                catch { }
            });
        }

        // ===== 战斗专家：爆发 =====

        // ===== 战斗专家「抗性」（2026-10-02 改造：75% 减伤 + 45% 移速，25 秒，CD 120 秒）=====

        private static void BreacherResist(Player player)
        {
            const float cd = 120f;
            const float duration = 25f;
            if (!CheckCooldown(player, 1, cd, "抗性")) return;

            // 技能期间伤害减免 75%（GocDamageHandler 读取此状态）
            BreacherGuardUntil[player.UserId] = DateTime.Now.AddSeconds(duration);

            // 移速 +45%，与 GOC 统一的 +20% 叠加
            try { player.EnableEffect(EffectType.MovementBoost, 45, duration); } catch { }

            player.ShowHint($"<color=#FF8844>[抗性]</color> 伤害减免 <color=#FFFF00>75%</color> · 移速 <color=#FFFF00>+45%</color>（{duration:0} 秒）", 5f);

            Timing.CallDelayed(duration, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;
                    player.ShowHint("<color=#AAAAAA>[抗性] 效果结束</color>", 2f);
                }
                catch { }
            });
        }

        // ===== 特战：技能1「急急急」（移速 +150%，15 秒，CD 45 秒）=====

        private static void SpecialOpsRush(Player player)
        {
            const float cd = 45f;
            if (!CheckCooldown(player, 1, cd, "急急急")) return;

            try { player.EnableEffect(EffectType.MovementBoost, 150, 15f); } catch { }
            player.ShowHint("<color=#44FF88>[急急急]</color> 移速提升 <color=#FFFF00>150%</color>（15 秒）", 5f);
        }

        // ===== 特战：技能2「左轮」（1 发 · 1500 伤害 · 命中敌人后消失，CD 120 秒）=====

        private static void SpecialOpsRevolver(Player player)
        {
            const float cd = 120f;
            if (!CheckCooldown(player, 2, cd, "左轮")) return;

            try
            {
                // 注意（2026-10-06 修复）：绝对不能改左轮的 MaxMagazineAmmo！
                // 左轮用的是 CylinderAmmoModule（弹巢模块），弹巢格数=弹匣容量，
                // 改成 1 会导致 RotateCylinder 除零 → 服务器判定 exploits → 踢人。
                // 保持原版 6 发弹匣；"一次性"效果由 GocGunFireWatcher 命中后移除武器实现。
                var item = player.AddItem(ItemType.GunRevolver);
                player.AddItem(ItemType.Ammo44cal);   // 备弹：打偏可换弹继续
                if (item != null) player.CurrentItem = item;
            }
            catch (Exception ex) { Log.Debug($"[GOC] 左轮发放失败: {ex.Message}"); }

            player.ShowHint("<color=#FFD700>[左轮]</color> 已获得左轮：<color=#FFFF00>1 发 / 1500 伤害</color> · 命中敌人后消失", 5f);
        }

        // ===== 医疗兵：范围治疗 =====

        private static void MedicAreaHeal(Player player)
        {
            const float cd = 30f;
            if (!CheckCooldown(player, 1, cd, "范围治疗")) return;

            Timing.RunCoroutine(AreaHealRoutine(player, MedicAreaRadius, 8f, 15f));
            ShowSkillUsed(player, "范围治疗", cd);
        }

        // ===== 医疗兵：肾上腺素 =====

        private static void MedicAdrenaline(Player player)
        {
            const float cd = 45f;
            if (!CheckCooldown(player, 2, cd, "肾上腺素")) return;

            int count = 0;
            foreach (var goc in GetGocMembers())
            {
                try
                {
                    goc.ArtificialHealth = Math.Max(goc.ArtificialHealth, 0f) + 50f;
                    goc.EnableEffect(EffectType.MovementBoost, 30, 10f);
                    count++;
                }
                catch { }
            }
            player.ShowHint($"<color=#00AEEF>[GOC]</color> <color=#FFD700>肾上腺素</color> 已释放，{count} 名队友获得 50 AHP + 30% 移速 <color=#AAAAAA>(冷却 {cd:0}s)</color>", 3f);
        }

        // ===== 指挥官：隐身 =====

        private static void CommanderInvisible(Player player)
        {
            const float cd = 60f;
            if (!CheckCooldown(player, 1, cd, "隐身")) return;

            try { player.EnableEffect(EffectType.Invisible, 15f); } catch (Exception ex) { Log.Debug($"[GOC] 隐身失败: {ex.Message}"); }
            ShowSkillUsed(player, "隐身", cd);
        }

        // ===== 指挥官：激励 =====

        private static void CommanderInspire(Player player)
        {
            const float cd = 120f;
            if (!CheckCooldown(player, 2, cd, "激励")) return;

            int count = 0;
            foreach (var goc in GetGocMembers())
            {
                try
                {
                    goc.ArtificialHealth = Math.Max(goc.ArtificialHealth, 0f) + 100f;
                    goc.EnableEffect(EffectType.MovementBoost, 35, 15f);
                    goc.EnableEffect(EffectType.Scp207, 1, 15f);   // 肾上腺素效果
                    count++;
                }
                catch { }
            }
            player.ShowHint($"<color=#00AEEF>[GOC]</color> <color=#FFD700>激励</color> 已释放，{count} 名 GOC 获得 35% 移速 + 100 AHP + 肾上腺素 <color=#AAAAAA>(冷却 {cd:0}s)</color>", 3f);
        }

        // ===== 奇术师：困住（技能1）=====

        /// <summary>奇术师-困住：方圆 10m 的所有敌人定身 15 秒（不能移动 + 视角锁定），冷却 100 秒</summary>
        private static void ThaumaturgeTrap(Player player)
        {
            const float cd = 100f;
            if (!CheckCooldown(player, 1, cd, "困住")) return;

            int count = 0;
            foreach (var target in Player.List)
            {
                try
                {
                    if (target == null || !target.IsConnected || !target.IsAlive) continue;
                    if (GocManager.IsGoc(target)) continue;                        // 队友不困
                    if (Vector3.Distance(player.Position, target.Position) > 10f) continue;
                    target.EnableEffect(EffectType.Ensnared, 15f);
                    target.ShowHint("<color=#B388FF>[奇术]</color> <color=#FFFFFF>你被奇术困住了！</color>", 3f);
                    count++;
                }
                catch { }
            }
            player.ShowHint($"<color=#00AEEF>[GOC]</color> <color=#FFD700>困住</color> 已释放，{count} 名敌人被定身 15 秒 <color=#AAAAAA>(冷却 {cd:0}s)</color>", 3f);
        }

        // ===== 奇术师：震击（技能2）=====

        /// <summary>奇术师-震击：周围 5m 的玩家获得雷击效果，每秒 -15 HP 持续 20 秒，冷却 100 秒</summary>
        private static void ThaumaturgeShock(Player player)
        {
            const float cd = 100f;
            if (!CheckCooldown(player, 2, cd, "震击")) return;

            int count = 0;
            foreach (var target in Player.List)
            {
                try
                {
                    if (target == null || !target.IsConnected || !target.IsAlive) continue;
                    if (GocManager.IsGoc(target)) continue;                        // 队友免震
                    if (Vector3.Distance(player.Position, target.Position) > 5f) continue;
                    Timing.RunCoroutine(ShockRoutine(target, player, 15f, 20f));
                    count++;
                }
                catch { }
            }
            player.ShowHint($"<color=#00AEEF>[GOC]</color> <color=#FFD700>震击</color> 已释放，{count} 名玩家遭受雷击 <color=#AAAAAA>(冷却 {cd:0}s)</color>", 3f);
        }

        /// <summary>雷击：每秒 -15 HP 持续 20 秒（走 Hurt 事件带攻击者，击杀正确归属奇术师）</summary>
        private static IEnumerator<float> ShockRoutine(Player target, Player attacker, float perSecond, float duration)
        {
            for (float t = 0f; t < duration; t += 1f)
            {
                yield return Timing.WaitForSeconds(1f);
                if (target == null || !target.IsConnected || !target.IsAlive) yield break;
                if (attacker == null || !attacker.IsConnected || !attacker.IsAlive) yield break;   // 奇术师死亡/离开则震击停止

                // 走 Hurt 事件（带攻击者）→ 致死时击杀正确归属奇术师，计入击杀
                target.Hurt(attacker, perSecond, Exiled.API.Enums.DamageType.Firearm);
                target.ShowHint($"<color=#FFFF00>[震击]</color> <color=#FF6666>-{perSecond:0} HP</color>", 1f);
            }
        }

        // ===== 通用协程 =====

        /// <summary>每秒回复 HP，持续指定秒数（队友离开后自动停止）</summary>
        private static IEnumerator<float> HealOverTime(Player player, float perSecond, float duration)
        {
            for (float t = 0f; t < duration; t += 1f)
            {
                yield return Timing.WaitForSeconds(1f);
                if (player == null || !player.IsConnected || !player.IsAlive) yield break;
                if (player.Health < player.MaxHealth)
                    player.Heal(perSecond);
            }
        }

        /// <summary>范围内所有 GOC 队友每秒回血，持续指定秒数</summary>
        private static IEnumerator<float> AreaHealRoutine(Player caster, float radius, float perSecond, float duration)
        {
            for (float t = 0f; t < duration; t += 1f)
            {
                yield return Timing.WaitForSeconds(1f);
                if (caster == null || !caster.IsConnected || !caster.IsAlive) yield break;

                foreach (var ally in GetGocMembers())
                {
                    try
                    {
                        if (ally.Health >= ally.MaxHealth) continue;
                        if (Vector3.Distance(caster.Position, ally.Position) > radius) continue;
                        ally.Heal(perSecond);
                    }
                    catch { }
                }
            }
        }

        /// <summary>取所有在线存活的 GOC 成员</summary>
        private static List<Player> GetGocMembers()
        {
            var list = new List<Player>();
            foreach (var kv in GocManager.Members)
            {
                var p = Player.Get(kv.Key);
                if (p != null && p.IsConnected && p.IsAlive) list.Add(p);
            }
            return list;
        }
    }
}
