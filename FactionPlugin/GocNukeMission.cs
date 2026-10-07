using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// GOC 核弹任务：
    ///   1. 只有 GOC 成员能开启核弹面板（ActivatingWarheadPanel）
    ///   2. 启动后把倒计时改为 120 秒（Warhead.DetonationTimer）
    ///   3. 剩余 30 秒时 Warhead.IsLocked = true（面板无法关闭核弹）
    ///   4. 剩余 5 秒撤离所有 GOC 成员（+10000 经验）
    /// </summary>
    public static class GocNukeMission
    {
        private static DateTime _startTime;
        private static bool _running;
        private static bool _evacuated;
        private static bool _locked;
        private static bool _leverWasOn;
        private static bool _cleared;
        private static bool _evacHinted;

        public const float WarheadDuration = 120f;
        public const float LockoutSeconds = 30f;
        public const float EvacuateSeconds = 5f;
        public const float EvacHintSeconds = 60f;
        public const int EvacuateExp = 10000;

        // ===== 面板交互：只有 GOC 能开（SCP-181 有 15% 幸运解锁）=====
        public static void OnActivatingWarheadPanel(ActivatingWarheadPanelEventArgs ev)
        {
            if (ev.Player == null) return;
            // 核弹面板限制属于特殊角色功能，只在 7779 生效
            if (!FactionPlugin.SpecialRolesEnabled) return;
            if (GocManager.IsGoc(ev.Player)) return;    // GOC 放行（原生逻辑会启动核弹）

            // SCP-181：15% 概率无卡解锁地表核弹面板
            if (Scp181Manager.Is181(ev.Player) && UnityEngine.Random.value < Scp181Manager.DoorChance)
            {
                ev.Player.ShowHint("<color=#FFD700>═══ [SCP-181 技能发动] ═══</color>\n<color=#44FF88>幸运降临！解锁了地表核弹面板！（15% 概率触发）</color>", 3f);
                return;    // 放行，原生逻辑继续启动核弹
            }

            ev.IsAllowed = false;
            ev.Player.ShowHint("<color=#FF4444>[系统] 只有 GOC 成员可以开启核弹</color>", 2f);
        }

        
        // ===== 每秒轮询（由 FactionPlugin.CooldownRoutine 调用）=====
        public static void MonitorTick()
        {
            try
            {
                // 核弹任务属于特殊角色功能，只在 7779 实例生效
                if (!FactionPlugin.SpecialRolesEnabled) return;

                bool inProgress = Warhead.IsInProgress;

                // 检测核弹启动：开启 GOC 任务并改倒计时
                if (inProgress && !_running)
                {
                    _running = true;
                    _evacuated = false;
                    _locked = false;
                    _startTime = DateTime.Now;

                    // 倒计时改为 120 秒
                    try { Warhead.DetonationTimer = WarheadDuration; }
                    catch (Exception ex) { Log.Warn($"[GOC] 修改核弹倒计时失败: {ex.Message}"); }

                    Map.Broadcast(10, "<color=#FF0000>[警报]</color> 欧米茄核弹已被 GOC 启动！倒计时 " + (int)WarheadDuration + " 秒，无法阻止！");
                    // 非战斗人员撤离窗口（用户要求 2026-10-02）：撤离即变观察者并 +1000 经验（EXP 插件 exp_per_escape）
                    Map.Broadcast(10, "<color=#FFD700>[撤离窗口开启]</color> 非战斗人员（D 级 / 科研）请立刻前往地表撤离点撤离，<color=#FFFF00>撤离成功 +1000 经验</color>！");
                    Log.Info("[GOC] 核弹任务开始（120s 计时）");
                }

                // ===== 拉杆检测：GOC 拉下核弹室拉杆（LeverStatus false→true）→ 直接启动引爆程序 =====
                // 注意：这是唯一启动方式（没有技能键）。GOC 是 Tutorial 载体，与拉杆交互后此检测负责点火。
                bool leverOn = Warhead.LeverStatus;
                if (leverOn && !_leverWasOn && !Warhead.IsInProgress)
                {
                    try
                    {
                        // 拉杆坐标（优先 lever，其次面板，最后退回原点）
                        Vector3 leverPos = Vector3.zero;
                        var sitePanel = Warhead.SitePanel;
                        if (sitePanel != null)
                        {
                            if (sitePanel.lever != null)
                                leverPos = sitePanel.lever.transform.position;
                            else
                                leverPos = sitePanel.transform.position;
                        }

                        Player gocNear = null;
                        foreach (var kv in GocManager.Members)
                        {
                            var p = Player.Get(kv.Key);
                            if (p == null || !p.IsConnected || !p.IsAlive) continue;

                            // 判定 1：GOC 在核弹室内（HczNuke）——最符合"拉杆"场景
                            bool inNukeRoom = false;
                            try { inNukeRoom = p.CurrentRoom != null && p.CurrentRoom.Type == Exiled.API.Enums.RoomType.HczNuke; }
                            catch { }

                            // 判定 2：坐标兜底（15m 内）
                            if (!inNukeRoom)
                                inNukeRoom = Vector3.Distance(p.Position, leverPos) <= 15f;

                            if (inNukeRoom) { gocNear = p; break; }
                        }

                        if (gocNear != null)
                        {
                            Warhead.Start();
                            Map.Broadcast(10, "<color=#FF0000>[警报]</color> 欧米茄核弹已被 GOC 启动！倒计时 120 秒，无法阻止！");
                            gocNear.ShowHint("<color=#FF0000>[GOC 任务]</color> 引爆程序已启动！120 秒后爆炸！准备撤离！", 4f);
                            Log.Info($"[GOC] {gocNear.Nickname} 拉下核弹拉杆，引爆程序已启动");
                        }
                        else
                        {
                            Log.Info("[GOC] 检测到核弹拉杆被拉下，但核弹室内没有 GOC 成员（不启动）");
                        }
                    }
                    catch (Exception ex) { Log.Warn($"[GOC] 拉杆检测失败: {ex.Message}"); }
                }
                _leverWasOn = leverOn;

                if (!_running) return;

                float remain = RemainingSeconds();

                // 剩余 30 秒：锁定核弹（面板无法关闭）
                if (remain <= LockoutSeconds && !_locked)
                {
                    _locked = true;
                    try
                    {
                        Warhead.IsLocked = true;
                        Map.Broadcast(5, "<color=#FF0000>[警报]</color> 核弹进入不可关闭阶段！剩余 30 秒！");
                        Log.Info("[GOC] 核弹已锁定，无法关闭");
                    }
                    catch (Exception ex) { Log.Debug($"[GOC] 锁定核弹失败: {ex.Message}"); }
                }

                // 剩余 5 秒：撤离所有 GOC
                if (remain <= EvacuateSeconds && !_evacuated)
                {
                    _evacuated = true;
                    EvacuateAllGoc();
                }

                // 剩余 60 秒：二次提醒非战斗人员撤离
                if (remain <= EvacHintSeconds && !_evacHinted)
                {
                    _evacHinted = true;
                    Map.Broadcast(6, "<color=#FFD700>[撤离窗口]</color> 还剩 60 秒！非战斗人员立即撤离（+1000 经验）！");
                }

                // 倒计时归零：强制清场 + 终结回合（用户要求 2026-10-02）
                if (remain <= 0f && !_cleared)
                {
                    _cleared = true;
                    ForceKillAllAndEndRound();
                    return;
                }

                // 爆炸后任务结束
                if (!inProgress && _evacuated)
                {
                    _running = false;
                    GocManager.OnRoundEnded();
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"[GOC] 核弹监控异常: {ex.Message}");
            }
        }

        // ===== 撤离 + 经验 =====
        private static void EvacuateAllGoc()
        {
            int count = 0;
            foreach (var kv in GocManager.Members.ToList())
            {
                var p = Player.Get(kv.Key);
                if (p == null || !p.IsConnected) continue;

                // +10000 经验
                try
                {
                    var expPlugin = ExperiencePlugin.ExperiencePlugin.Instance;
                    var data = expPlugin?.DataManager?.GetPlayerData(p.UserId);
                    if (data != null)
                    {
                        int baseExp = expPlugin.Config.BaseExpPerLevel;
                        data.AddExperience(EvacuateExp, baseExp);
                        expPlugin.DataManager.SaveAllData();
                        p.ShowHint($"<color=#FFD700>[GOC 任务完成]</color> 核弹任务撤离成功！<color=#FFD700>+{EvacuateExp} 经验</color>", 8f);
                    }
                }
                catch (Exception ex) { Log.Error($"[GOC] 撤离经验发放失败: {ex.Message}"); }

                // 撤离为观察者
                if (p.IsAlive)
                {
                    try { p.Role.Set(RoleTypeId.Spectator, SpawnReason.Escaped); } catch { }
                }
                count++;
            }

            Map.Broadcast(8, $"<color=#FFD700>[GOC 任务完成]</color> {count} 名 GOC 成员成功撤离，每人获得 {EvacuateExp} 经验！");
            GocManager.OnRoundEnded();
        }

        /// <summary>
        /// 倒计时归零：强制杀死全部存活玩家并终结回合（用户要求 2026-10-02）。
        /// 原生核弹只杀设施内人员，地表可能幸存 → 这里补一次全图清场，确保回合干净结束。
        /// </summary>
        private static void ForceKillAllAndEndRound()
        {
            try
            {
                Map.Broadcast(8, "<color=#FF0000>[核弹引爆]</color> 欧米茄核弹爆炸，所有人员阵亡！");

                int killed = 0;
                foreach (var p in Player.List.ToList())
                {
                    if (p == null || !p.IsConnected || !p.IsAlive) continue;
                    try { p.Kill("GOC 欧米茄核弹引爆"); killed++; } catch { }
                }
                Log.Info($"[GOC] 核弹引爆：强制清场 {killed} 人");

                // 终结回合
                try { Round.EndRound(); }
                catch (Exception ex) { Log.Warn($"[GOC] 终结回合失败: {ex.Message}"); }
            }
            catch (Exception ex) { Log.Error($"[GOC] 核弹清场失败: {ex.Message}"); }
            finally
            {
                _running = false;
                GocManager.OnRoundEnded();
            }
        }

        /// <summary>
        /// 玩家撤离（Escaped 事件，2026-10-02）。
        /// 核弹任务进行期间，非战斗人员（D 级 / 科研）撤离 → 强制转为观察者。
        /// 原因：原版 D 级逃出会被招募为 MTF，用户要求撤离者直接变观察者。
        /// 撤离经验（1000）由 ExperiencePlugin 的 exp_per_escape 发放。
        /// </summary>
        public static void OnPlayerEscaped(Player player)
        {
            try
            {
                if (!_running) return;                       // 只在核弹任务期间生效
                if (player == null || !player.IsConnected) return;

                // 撤离窗口期的撤离者：提示 + 强制转为观察者
                // （覆盖原版"D 级逃出→被招募为 MTF"的行为；经验 1000 由 ExperiencePlugin 发放）
                player.ShowHint("<color=#FFD700>[撤离成功]</color> 你已安全撤离！<color=#FFFF00>+1000 经验</color> · 转为观察者", 6f);

                Timing.CallDelayed(0.3f, () =>
                {
                    try
                    {
                        if (player == null || !player.IsConnected) return;
                        if (player.Role.Type == RoleTypeId.Spectator) return;
                        player.Role.Set(RoleTypeId.Spectator, SpawnReason.Escaped);
                        Log.Info($"[GOC] 核弹期间撤离：{player.Nickname} → 观察者");
                    }
                    catch { }
                });
            }
            catch (Exception ex) { Log.Debug($"[GOC] 撤离处理失败: {ex.Message}"); }
        }

        // ===== 剩余时间（基于任务开始时刻的虚拟计时）=====
        public static float RemainingSeconds()
        {
            if (!_running) return 0f;
            float elapsed = (float)(DateTime.Now - _startTime).TotalSeconds;
            return Math.Max(0f, WarheadDuration - elapsed);
        }

        public static void Reset()
        {
            _running = false;
            _evacuated = false;
            _locked = false;
            _leverWasOn = false;
            _cleared = false;
            _evacHinted = false;
        }
    }
}
