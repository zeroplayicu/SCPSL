using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using UnityEngine;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace FactionPlugin
{
    /// <summary>GOC（全球超自然联盟）兵种</summary>
    public enum GocRoleType
    {
        Soldier,      // 士兵
        Vanguard,     // 先锋（2026-10-02 新增：75% 移速，技能1 超负荷）
        Heavy,        // 重装
        Breacher,     // 战斗专家
        SpecialOps,   // 特战（2026-10-02 新增：囚鸟+手枪，急急急/左轮）
        Medic,        // 医疗兵
        Commander,    // 指挥官
        Thaumaturge   // 奇术师（困住/震击）
    }

    /// <summary>
    /// GOC（全球超自然联盟）阵营管理器。
    /// 敌对关系：SCP + 所有人类（含九尾狐、D 级、科研），GOC 内部无友伤。
    /// 载体角色使用 Chaos 系列（原生即敌对 MTF/D 级/科研/SCP），通过 RankName/CustomInfo 显示 GOC 标识。
    /// </summary>
    public static class GocManager
    {
        /// <summary>当前 GOC 成员：UserId → 兵种</summary>
        public static readonly Dictionary<string, GocRoleType> Members = new Dictionary<string, GocRoleType>();

        /// <summary>GOC 技能按键绑定（由 FactionPlugin 在启用时创建）</summary>

        /// <summary>混沌出生点坐标缓存（每回合首次获取后复用，地图每回合随机所以回合结束要清）</summary>
        private static Vector3? _chaosSpawnPos;

        /// <summary>本回合是否已经刷新过（每回合最多刷一次）</summary>
        private static bool _spawnedThisRound;

        /// <summary>刷新用兵种顺序（2026-10-02 加先锋/特战，满编 9 人）</summary>
        private static readonly GocRoleType[] SpawnOrder =
        {
            GocRoleType.Soldier,
            GocRoleType.Soldier,
            GocRoleType.Vanguard,
            GocRoleType.Heavy,
            GocRoleType.Breacher,
            GocRoleType.SpecialOps,
            GocRoleType.Medic,
            GocRoleType.Commander,
            GocRoleType.Thaumaturge
        };

        // ===== 查询 =====

        public static bool IsGoc(Player player) => player != null && Members.ContainsKey(player.UserId);
        public static bool IsGoc(string userId) => !string.IsNullOrEmpty(userId) && Members.ContainsKey(userId);

        public static string GetRoleName(GocRoleType t)
        {
            switch (t)
            {
                case GocRoleType.Soldier: return "士兵";
                case GocRoleType.Vanguard: return "先锋";
                case GocRoleType.Heavy: return "重装";
                case GocRoleType.Breacher: return "战斗专家";
                case GocRoleType.SpecialOps: return "特战";
                case GocRoleType.Medic: return "医疗兵";
                case GocRoleType.Commander: return "指挥官";
                case GocRoleType.Thaumaturge: return "奇术师";
                default: return "成员";
            }
        }

        // ===== 回合生命周期 =====

        public static void OnRoundStarted()
        {
            Members.Clear();
            _spawnedThisRound = false;
            GocSkillManager.Reset();
        }

        public static void OnRoundEnded()
        {
            Members.Clear();
            _spawnedThisRound = false;
            _chaosSpawnPos = null;
            GocSkillManager.Reset();
            ClearMoveCache();
        }

        /// <summary>玩家离开时移除记录</summary>
        public static void OnPlayerLeft(Player player)
        {
            if (player == null) return;
            Members.Remove(player.UserId);
            LastPositions.Remove(player.UserId);
            LastMoveTime.Remove(player.UserId);
        }

        /// <summary>
        /// 彻底清除 GOC 标志与身份：RankName / CustomInfo / 成员记录 / 技能按键。
        /// 在玩家死亡、离开、被其他角色（999/181 等）覆盖变身时调用，防止标志残留。
        /// </summary>
        public static void ClearBadge(Player player)
        {
            try
            {
                if (player == null) return;
                Members.Remove(player.UserId);
                try { player.RankName = null; } catch { }
                try { player.CustomInfo = null; } catch { }
            }
            catch { }
        }

        // ===== 刷新 =====

        /// <summary>
        /// 尝试刷新 GOC：
        ///   - 从阴间（死亡/观战）玩家中随机选取，最多 6 个、最少 0 个（0 个则不刷）
        ///   - 可以重复刷（force=true 时无任何限制；自动刷新每回合 150 秒一次）
        ///   - 不检查存活人类数量
        /// 刷新后出生在混沌分裂者出生点。
        /// </summary>
        public static bool TrySpawnGoc(bool force = false)
        {
            try
            {
                // 特殊角色只在 7779 实例启用（用户要求 2026-10-02）
                if (!FactionPlugin.SpecialRolesEnabled) return false;

                if (!force && _spawnedThisRound) return false;
                if (!Round.IsStarted && !force) return false;

                // 候选：阴间（死亡/观战）玩家 —— GOC 从阴间复活
                var candidates = Player.List
                    .Where(p => p != null && p.IsConnected && !p.IsAlive && !p.IsScp && !IsGoc(p) && !Scp999Manager.IsScp999(p))
                    .ToList();

                // 最多 6 个，有多少刷多少（0 个则不刷）
                var picked = candidates.OrderBy(x => Guid.NewGuid()).Take(SpawnOrder.Length).ToList();
                if (picked.Count == 0)
                {
                    Log.Info("[GOC] 刷新跳过：阴间没有可复活的玩家");
                    return false;
                }

                _spawnedThisRound = true;

                // 全服广播
                Map.Broadcast(12, "<color=#00AEEF>[全球超自然联盟]</color> 注意：全球超自然联盟通过特殊手段进入设施，所有人员保护欧米茄核弹头！");

                for (int i = 0; i < picked.Count; i++)
                    SpawnGocMember(picked[i], SpawnOrder[i % SpawnOrder.Length]);

                Log.Info($"[GOC] 已从阴间复活 GOC 阵营，共 {picked.Count} 人");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[GOC] 刷新失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>强制刷新 GOC（供管理命令 `sx goc` 调用，忽略对局时间与"每回合一次"限制）</summary>
        public static bool ForceSpawn()
        {
            Log.Info("[GOC] 管理员强制刷新 GOC");
            return TrySpawnGoc(force: true);
        }

        /// <summary>
        /// 强制刷新指定兵种的单名 GOC（供 `sx goc sb/zz/zdzj/ylb/zhg` 调用）。
        /// 优先从阴间选人；阴间没人则用存活人类；都没有则失败。
        /// </summary>
        public static bool ForceSpawnSingle(GocRoleType roleType, out string response)
        {
            try
            {
                Player target = Player.List
                    .Where(p => p != null && p.IsConnected && !p.IsAlive && !p.IsScp && !IsGoc(p) && !Scp999Manager.IsScp999(p))
                    .OrderBy(p => Guid.NewGuid())
                    .FirstOrDefault();

                if (target == null)
                {
                    target = Player.List
                        .Where(p => p != null && p.IsConnected && p.IsAlive && !p.IsScp && !IsGoc(p))
                        .OrderBy(p => Guid.NewGuid())
                        .FirstOrDefault();
                }

                if (target == null)
                {
                    response = "没有可用的玩家（阴间和存活队列都为空）";
                    return false;
                }

                SpawnGocMember(target, roleType);
                response = $"{target.Nickname} 已变身为 GOC {GetRoleName(roleType)}";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[GOC] 单兵种刷新失败: {ex.Message}");
                response = "刷新失败: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 把一名玩家转变为 GOC 成员。
        /// 外观全程保持 Tutorial 特殊角色（无阵营 = 敌对所有），出生即传送到混沌分裂者出生点。
        /// </summary>
        private static void SpawnGocMember(Player player, GocRoleType roleType)
        {
            if (player == null || !player.IsConnected) return;

            Members[player.UserId] = roleType;

            player.Role.Set(RoleTypeId.Tutorial, SpawnReason.ForceClass, RoleSpawnFlags.None);

            Timing.CallDelayed(0.6f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;

                    // 传送到混沌出生点（外观全程保持 Tutorial）
                    if (_chaosSpawnPos.HasValue)
                    {
                        player.Position = _chaosSpawnPos.Value;
                        Resupply(player, roleType);
                    }
                    else
                    {
                        // 首次：临时变 Chaos 等出生传送完成 → 捕获坐标 → 变回 Tutorial 传回该点
                        Timing.RunCoroutine(CaptureChaosSpawnRoutine(player, roleType));
                    }
                }
                catch (Exception ex) { Log.Error($"[GOC] 刷新传送失败: {ex.Message}"); }
            });
        }

        /// <summary>
        /// 临时变 Chaos 出生 → 等位置稳定后捕获混沌出生点坐标（修订 2026-10-02）。
        /// 旧实现固定延迟 0.3 秒，可能早于出生传送完成，导致缓存到错误坐标（Tutorial 出生点）。
        /// </summary>
        private static IEnumerator<float> CaptureChaosSpawnRoutine(Player player, GocRoleType roleType)
        {
            try
            {
                if (player == null || !player.IsConnected) yield break;

                player.Role.Set(RoleTypeId.ChaosRifleman, SpawnReason.ForceClass, RoleSpawnFlags.None);

                // 轮询等待出生传送完成：连续 3 次位置不变视为已稳定（最多 4 秒）
                Vector3? last = null;
                int stableCount = 0;
                for (int i = 0; i < 26; i++)
                {
                    yield return Timing.WaitForSeconds(0.15f);
                    if (player == null || !player.IsConnected) yield break;

                    Vector3 cur = player.Position;
                    if (last.HasValue && (cur - last.Value).sqrMagnitude < 0.0001f)
                    {
                        stableCount++;
                        if (stableCount >= 3) break;
                    }
                    else stableCount = 0;
                    last = cur;
                }

                if (player == null || !player.IsConnected) yield break;

                _chaosSpawnPos = player.Position;
                Log.Info($"[GOC] 捕获混沌出生点坐标: {_chaosSpawnPos.Value}");

                // 变回 Tutorial（外观），再传回捕获到的混沌出生点
                player.Role.Set(RoleTypeId.Tutorial, SpawnReason.ForceClass, RoleSpawnFlags.None);
                yield return Timing.WaitForSeconds(0.6f);

                if (player == null || !player.IsConnected) yield break;
                player.Position = _chaosSpawnPos.Value;
                Resupply(player, roleType);
            }
            finally { }
        }

        /// <summary>阶段二抵达混沌出生点后的补给：重发血量/标识/装备/按键（Role 变更会重置背包与标识）</summary>
        private static void Resupply(Player player, GocRoleType roleType)
        {
            try
            {
                if (player == null || !player.IsConnected || !player.IsAlive) return;

                Members[player.UserId] = roleType;   // 防御性恢复成员记录
                player.MaxHealth = 200f;
                player.Health = 200f;
                // 用户要求（2026-10-01 11:27）删除头顶头衔显示
                player.RankName = null;
                player.CustomInfo = null;
                GocLoadout.Apply(player, roleType);

                player.ShowHint("<color=#00AEEF>[GOC]</color> 已抵达混沌出生点，开始行动！", 3f);
                Log.Info($"[GOC] {player.Nickname} 已抵达混沌出生点（外观保持 Tutorial）");
            }
            catch (Exception ex) { Log.Error($"[GOC] 阶段二初始化失败: {ex.Message}"); }
        }

        /// <summary>
        /// 显示 GOC 图标（由 PNG 转换成的彩色文本图像，持续 8 秒）。
        /// 字号按"目标像素宽度 / 图标字符宽度"自动计算，保证不同尺寸图标占据相近的屏幕面积。
        /// </summary>
        private static void ShowGocIcon(Player player)
        {
            try
            {
                if (player == null || !player.IsConnected) return;
                if (string.IsNullOrEmpty(GocAssets.Icon)) return;

                const int targetPixelWidth = 320;   // 目标显示宽度（1920 屏宽下约 1/6）
                int fontSize = Math.Max(4, targetPixelWidth / Math.Max(1, GocAssets.IconWidth));

                var disp = PlayerDisplay.Get(player);
                disp.RemoveHint("goc_icon");
                disp.ShowHint(new HsmHint
                {
                    Id = "goc_icon",
                    Text = GocAssets.Icon,
                    FontSize = fontSize,
                    YCoordinate = 420,
                    Alignment = HintAlignment.Center
                }, 8f);
            }
            catch (Exception ex)
            {
                Log.Debug($"[GOC] 图标显示失败: {ex.Message}");
            }
        }

        /// <summary>传送到管理塔附近房间（核弹室）；按房间名匹配，找不到时回退 HCZ 深处</summary>
        private static void TeleportToGocRoom(Player player)
        {
            try
            {
                // 按房间名搜索核弹室（Warhead / Nuke），避免依赖具体 RoomType 枚举名
                Room target = Room.List.FirstOrDefault(r =>
                    r != null && !string.IsNullOrEmpty(r.Name) &&
                    (r.Name.IndexOf("Warhead", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     r.Name.IndexOf("Nuke", StringComparison.OrdinalIgnoreCase) >= 0));

                if (target == null || target.Position == Vector3.zero)
                {
                    // 回退：SCP-049 房间（HCZ 深处）
                    target = Room.List.FirstOrDefault(r =>
                        r != null && !string.IsNullOrEmpty(r.Name) &&
                        r.Name.IndexOf("Hcz049", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                if (target == null) return;

                // 在房间中心附近散开，避免重叠
                float angle = (float)(UnityEngine.Random.value * Math.PI * 2);
                float radius = 1.5f;
                Vector3 offset = new Vector3(
                    (float)Math.Cos(angle) * radius,
                    1f,
                    (float)Math.Sin(angle) * radius);

                player.Position = target.Position + offset;
            }
            catch (Exception ex)
            {
                Log.Debug($"[GOC] 传送失败: {ex.Message}");
            }
        }

        /// <summary>判断 GOC 与其他单位的敌对关系（用于伤害拦截）</summary>
        public static bool IsHostileTo(Player attacker, Player victim)
        {
            if (attacker == null || victim == null) return false;
            // GOC 内部不互相伤害
            if (IsGoc(attacker) && IsGoc(victim)) return false;
            // GOC 对任何非 GOC（SCP / 人类）都可造成伤害
            return true;
        }

        // ===== 可恢复护盾（Hume Shield，非 AHP）=====

        /// <summary>重装护盾上限</summary>
        public const float HeavyShieldMax = 300f;
        /// <summary>指挥官护盾上限</summary>
        public const float CommanderShieldMax = 450f;
        /// <summary>奇术师护盾上限（同时是特制 A7 的弹药池：1HS=1发，子弹无限靠护盾供弹）</summary>
        public const float ThaumaturgeShieldMax = 150f;
        /// <summary>每秒护盾恢复量</summary>
        public const float ShieldRegenPerSecond = 5f;

        /// <summary>上次位置（移动检测）</summary>
        private static readonly Dictionary<string, Vector3> LastPositions = new Dictionary<string, Vector3>();
        /// <summary>上次移动时间（静止 5 秒以上才开始回盾）</summary>
        private static readonly Dictionary<string, DateTime> LastMoveTime = new Dictionary<string, DateTime>();
        /// <summary>静止多少秒后开始回盾</summary>
        public const double StandStillSeconds = 5.0;

        /// <summary>
        /// GOC 护盾恢复（0.1 秒高频调用）：重装/指挥官/奇术师。
        /// 条件：玩家站立不动 5 秒以上才开始回盾（移动会刷新计时）。
        /// </summary>
        public static void TickShieldRegen(float regenPerTick)
        {
            try
            {
                if (Members.Count == 0) return;
                var now = DateTime.Now;
                var removeIds = new List<string>();

                foreach (var kv in Members)
                {
                    var p = Player.Get(kv.Key);
                    if (p == null || !p.IsConnected || !p.IsAlive)
                    {
                        removeIds.Add(kv.Key);
                        continue;
                    }

                    // ===== 移动检测：位置变化视为移动 =====
                    if (LastPositions.TryGetValue(kv.Key, out Vector3 oldPos))
                    {
                        if ((p.Position - oldPos).sqrMagnitude > 0.01f)
                            LastMoveTime[kv.Key] = now;
                    }
                    else
                    {
                        LastMoveTime[kv.Key] = now;   // 首次记录，视为刚移动
                    }
                    LastPositions[kv.Key] = p.Position;

                    // ===== 静止 5 秒以上才回盾 =====
                    if (!LastMoveTime.TryGetValue(kv.Key, out DateTime lastMove)) continue;
                    if ((now - lastMove).TotalSeconds < StandStillSeconds) continue;

                    float max = GetShieldMax(kv.Value);
                    if (max <= 0f) continue;
                    if (p.HumeShield < max)
                        p.HumeShield = Math.Min(max, p.HumeShield + regenPerTick);
                }

                foreach (var id in removeIds)
                {
                    LastPositions.Remove(id);
                    LastMoveTime.Remove(id);
                }
            }
            catch { }
        }

        /// <summary>回合结束/清除成员时清理移动检测缓存</summary>
        public static void ClearMoveCache()
        {
            LastPositions.Clear();
            LastMoveTime.Clear();
        }

        /// <summary>取玩家的护盾上限（0 表示无护盾）</summary>
        public static float GetShieldMax(GocRoleType roleType)
        {
            switch (roleType)
            {
                case GocRoleType.Heavy: return HeavyShieldMax;
                case GocRoleType.Commander: return CommanderShieldMax;
                case GocRoleType.Thaumaturge: return ThaumaturgeShieldMax;
                default: return 0f;
            }
        }
    }
}
