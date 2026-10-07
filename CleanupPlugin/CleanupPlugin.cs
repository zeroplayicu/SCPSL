using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Roles;
using MEC;

namespace CleanupPlugin
{
    public class CleanupPlugin : Plugin<CleanupConfig>
    {
        public static CleanupPlugin Instance { get; private set; }

        public override string Name => "CleanupPlugin";
        public override string Author => "Developer";
        public override string Prefix => "cleanup";

        // MEC 协程替代 System.Timers.Timer：主线程执行，消除跨线程操作 Player/Ragdoll/Pickup 的竞态
        private CoroutineHandle _cleanupCoroutine;
        private CoroutineHandle _thresholdCoroutine;
        private CoroutineHandle _countdownCoroutine;
        private int _countdownRemaining;
        private bool _isCountingDown;

        private HashSet<ItemType> _protectedItemTypes;
        private HashSet<RoomType> _protectedRoomTypes;

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            ParseConfig();

            // 每N秒启动一次清理流程（先警告再清理）
            _cleanupCoroutine = Timing.RunCoroutine(CleanupRoutine());

            // 高频监控：掉落物超过阈值则提前清扫（不等到下个周期）
            _thresholdCoroutine = Timing.RunCoroutine(ThresholdRoutine());

            Log.Info($"{Name} 加载完成（每{Config.CleanupInterval}秒自动清理，尸体>{Config.PickupThreshold}提前清扫，清理掉落物:{(Config.CleanPickups ? "开" : "关")}，警告{Config.WarningCountdown}秒）");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Instance = null;
            Timing.KillCoroutines(_cleanupCoroutine);
            Timing.KillCoroutines(_thresholdCoroutine);
            Timing.KillCoroutines(_countdownCoroutine);
            _isCountingDown = false;

            base.OnDisabled();
        }

        private IEnumerator<float> CleanupRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.CleanupInterval);
                try { CleanupCycleCheck(); }
                catch (Exception ex) { Log.Error($"清理周期出错: {ex.Message}"); }
            }
        }

        private IEnumerator<float> ThresholdRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(5f);
                try { ThresholdCheck(); }
                catch (Exception ex) { Log.Error($"阈值检查出错: {ex.Message}"); }
            }
        }

        private void ParseConfig()
        {
            // 解析受保护的物品类型
            _protectedItemTypes = new HashSet<ItemType>();
            if (!string.IsNullOrEmpty(Config.ProtectedItemTypes))
            {
                foreach (var typeName in Config.ProtectedItemTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Enum.TryParse<ItemType>(typeName.Trim(), out var itemType))
                    {
                        _protectedItemTypes.Add(itemType);
                    }
                    else
                    {
                        Log.Warn($"[配置] 未知物品类型: {typeName.Trim()}");
                    }
                }
            }

            // 强制保护 SCP-127（ItemType 枚举名是 GunSCP127，即使配置写错 SCP127 也仍受保护）
            _protectedItemTypes.Add(ItemType.GunSCP127);

            // 强制保护：血包/医疗类 + 所有权限卡（2026-10-02 用户要求，不依赖 yml 配置）
            foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
            {
                string n = t.ToString();
                if (n.StartsWith("Keycard"))                      // 全部权限卡
                    _protectedItemTypes.Add(t);
            }
            _protectedItemTypes.Add(ItemType.Medkit);             // 医疗包
            _protectedItemTypes.Add(ItemType.Painkillers);        // 止痛药
            _protectedItemTypes.Add(ItemType.Adrenaline);         // 肾上腺素

            // 解析受保护的区域类型
            _protectedRoomTypes = new HashSet<RoomType>();
            if (!string.IsNullOrEmpty(Config.ProtectedRoomTypes))
            {
                foreach (var roomName in Config.ProtectedRoomTypes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Enum.TryParse<RoomType>(roomName.Trim(), out var roomType))
                    {
                        _protectedRoomTypes.Add(roomType);
                    }
                    else
                    {
                        Log.Warn($"[配置] 未知区域类型: {roomName.Trim()}");
                    }
                }
            }

            if (Config.Debug)
            {
                Log.Debug($"[配置] 受保护物品: {string.Join(", ", _protectedItemTypes)}");
                Log.Debug($"[配置] 受保护区域: {string.Join(", ", _protectedRoomTypes)}");
            }
        }

        private void CleanupCycleCheck()
        {
            if (_isCountingDown)
            {
                if (Config.Debug) Log.Debug("[清理] 上一轮清理仍在进行，跳过");
                return;
            }

            // 检查游戏是否在运行
            if (Player.List.Count() == 0 || !Round.IsStarted)
            {
                if (Config.Debug) Log.Debug("[清理] 游戏未开始或无玩家，跳过");
                return;
            }

            // 开局免清扫期：回合开始后前 N 秒不触发自动清扫
            if (Round.ElapsedTime.TotalSeconds < Config.GracePeriodSeconds)
            {
                if (Config.Debug) Log.Debug($"[清理] 开局免清扫期(已进行{Round.ElapsedTime.TotalSeconds:F0}s < {Config.GracePeriodSeconds}s)，跳过");
                return;
            }

            int ragdollCount = SafeRagdollCount();
            if (ragdollCount < 0) return; // 统计失败，本周期跳过

            int pickupCount = 0;
            if (Config.CleanPickups)
            {
                try { pickupCount = Pickup.List.Count(); } catch { }
            }

            if (Config.Debug) Log.Debug($"[清理] 当前尸体数量: {ragdollCount}，掉落物: {pickupCount}");

            // 有尸体 或 有掉落物 都触发清理（2026-10-02：原来只看尸体，导致没有尸体时不扫地面物品）
            if (ragdollCount > 0 || pickupCount > 0)
            {
                Log.Info($"[清理] 启动清理流程（尸体: {ragdollCount}，掉落物: {pickupCount}）");
                _isCountingDown = true;
                StartCountdown();
            }
            else
            {
                if (Config.Debug) Log.Debug("[清理] 无尸体无掉落物，跳过");
            }
        }

        /// <summary>
        /// 安全获取当前尸体数量。
        /// Ragdoll.List 是游戏实时集合，MEC 已在主线程执行，正常不会遇到并发修改；
        /// 保留快照+try-catch 兜底防其他插件线程干扰。
        /// </summary>
        private int SafeRagdollCount()
        {
            try
            {
                // 先快照成数组，避免在 LINQ 延迟枚举过程中集合被修改
                var snapshot = Exiled.API.Features.Ragdoll.List.ToArray();
                return snapshot.Length;
            }
            catch (InvalidOperationException)
            {
                // 枚举期间集合被修改（外部线程干扰）：MEC 已主线程化，此分支几乎不可达，
                // 直接放弃本周期统计（普通方法中无法让帧）
                return -1;
            }
        }

        private void ThresholdCheck()
        {
            // 上一轮清理仍在进行则跳过
            if (_isCountingDown) return;

            // 检查游戏是否在运行
            if (Player.List.Count() == 0 || !Round.IsStarted)
            {
                if (Config.Debug) Log.Debug("[清理] 游戏未开始或无玩家，跳过");
                return;
            }

            // 开局免清扫期：回合开始后前 N 秒不触发自动清扫
            if (Round.ElapsedTime.TotalSeconds < Config.GracePeriodSeconds)
            {
                if (Config.Debug) Log.Debug($"[清理] 开局免清扫期(已进行{Round.ElapsedTime.TotalSeconds:F0}s < {Config.GracePeriodSeconds}s)，跳过");
                return;
            }

            int ragdollCount = SafeRagdollCount();
            if (ragdollCount < 0) return; // 统计失败，本 tick 跳过

            if (ragdollCount > Config.PickupThreshold)
            {
                Log.Info($"[清理] 尸体超过阈值({ragdollCount}>{Config.PickupThreshold})，提前清扫！");
                _isCountingDown = true;
                StartCountdown();
            }
        }

        private void StartCountdown()
        {
            _countdownRemaining = Config.WarningCountdown;

            if (_countdownRemaining <= 0)
            {
                // 无需倒计时，直接清理
                ExecuteCleanup();
                return;
            }

            ShowWarning(_countdownRemaining);
            _countdownCoroutine = Timing.RunCoroutine(CountdownRoutine());
        }

        private IEnumerator<float> CountdownRoutine()
        {
            while (_isCountingDown)
            {
                yield return Timing.WaitForSeconds(1f);
                _countdownRemaining--;

                if (_countdownRemaining > 0)
                {
                    ShowWarning(_countdownRemaining);
                }
                else
                {
                    ExecuteCleanup();
                    yield break;
                }
            }
        }

        private void ShowWarning(int seconds)
        {
            try
            {
                // 屏幕文字提示（已取消 CASSIE 语音广播）
                string msg = Config.WarningTemplate.Replace("{time}", seconds.ToString());
                var players = Player.List.ToArray();
                foreach (var p in players)
                {
                    if (p == null) continue;
                    if (p.ReferenceHub == null) continue;
                    if (!p.IsConnected) continue;
                    p.ClearBroadcasts();
                    p.Broadcast(2, msg, Broadcast.BroadcastFlags.Normal);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"警告广播出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 立即执行一次完整清理（供 RA 命令 "ql" 调用，2026-10-02）。
        /// 跳过免清扫期 / 触发条件 / 倒计时警告，直接清理。
        /// </summary>
        public void ForceCleanupNow()
        {
            try
            {
                Log.Info("[清理] 管理员手动触发完整清理");
                ExecuteCleanup();
            }
            catch (Exception ex) { Log.Error($"手动清理失败: {ex.Message}"); }
        }

        private void ExecuteCleanup()
        {
            try
            {
                int removed = 0, skippedScp = 0, skippedRoom = 0;
                int ragdollRemoved = 0;

                // 清理掉落物（仅当 Config.CleanPickups==true 时执行；默认 false=只清理尸体）
                if (Config.CleanPickups)
                {
                    var pickups = Pickup.List.ToList();
                    foreach (var pickup in pickups)
                    {
                        if (pickup == null || !pickup.IsSpawned)
                            continue;

                        // 1. 跳过SCP物品/武器
                        if (_protectedItemTypes.Contains(pickup.Type))
                        {
                            skippedScp++;
                            if (Config.Debug) Log.Debug($"[清理] 跳过SCP物品: {pickup.Type}");
                            continue;
                        }

                        // 额外检查：类型名称包含SCP、GunSCP(SCP武器)或MicroHID的也跳过
                        string typeName = pickup.Type.ToString();
                        if (typeName.StartsWith("SCP") || typeName.StartsWith("GunSCP") ||
                            typeName == "MicroHID" ||
                            typeName == "Jailbird" || typeName == "AntiSCP207")
                        {
                            skippedScp++;
                            if (Config.Debug) Log.Debug($"[清理] 跳过SCP物品(名称匹配): {pickup.Type}");
                            continue;
                        }

                        // 2. 跳过受保护房间内的物品
                        try
                        {
                            var room = pickup.Room;
                            if (room != null && _protectedRoomTypes.Contains(room.Type))
                            {
                                skippedRoom++;
                                if (Config.Debug) Log.Debug($"[清理] 跳过受保护房间物品: {pickup.Type} @ {room.Type}");
                                continue;
                            }
                        }
                        catch { /* 无法获取房间信息，继续清理 */ }

                        // 3. 收容区域(HCZ)除房间内的物品不进行清理
                        // 即: HCZ 走廊/过道的物品跳过清理, 但 HCZ 房间内的物品正常清理
                        try
                        {
                            var room = pickup.Room;
                            if (room != null && room.Zone == ZoneType.HeavyContainment
                                && !_protectedRoomTypes.Contains(room.Type))
                            {
                                skippedRoom++;
                                if (Config.Debug) Log.Debug($"[清理] 跳过HCZ走廊物品: {pickup.Type} @ {room.Type}");
                                continue;
                            }
                        }
                        catch { }

                        // 通过检查，销毁该物品
                        pickup.Destroy();
                        removed++;
                    }
                }

                // 清理尸体
                if (Config.CleanRagdolls)
                {
                    var ragdolls = Exiled.API.Features.Ragdoll.List.ToList();
                    foreach (var ragdoll in ragdolls)
                    {
                        if (ragdoll == null) continue;

                        // 跳过受保护房间内的尸体
                        try
                        {
                            var room = ragdoll.Room;
                            if (room != null && _protectedRoomTypes.Contains(room.Type))
                                continue;
                        }
                        catch { }

                        ragdoll.Destroy();
                        ragdollRemoved++;
                    }
                }

                Log.Info($"[清理完成] 清除 {removed} 个掉落物 + {ragdollRemoved} 具尸体 " +
                         $"(跳过SCP物品: {skippedScp}, 受保护区域: {skippedRoom})");

                // 广播清理完成（屏幕文字提示，已取消 CASSIE 语音广播）
                if (Config.KeepScreenWarning)
                {
                    var onlinePlayers = Player.List.ToArray();
                    foreach (var p in onlinePlayers)
                    {
                        if (p == null) continue;
                        if (p.ReferenceHub == null || !p.IsConnected) continue;
                        p.ClearBroadcasts();
                        p.Broadcast(3, Config.CleanupDoneMessage, Broadcast.BroadcastFlags.Normal);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"清理执行出错: {ex.Message}");
            }
            finally
            {
                _isCountingDown = false;
            }
        }

        /// <summary>
        /// 手动清理：由管理员指令(clanr)调用。强制清理所有掉落物和尸体，
        /// 但始终跳过 SCP/特殊武器物品与受保护房间。
        /// 不受 Config.CleanPickups 限制（即使自动清扫只清尸体，手动指令也清掉落物+尸体）。
        /// </summary>
        public void ManualCleanup(out int pickupRemoved, out int ragdollRemoved, out int skippedScp)
        {
            pickupRemoved = 0;
            ragdollRemoved = 0;
            skippedScp = 0;
            int skippedRoom = 0;

            try
            {
                // ---- 清理所有掉落物（跳过 SCP/特殊武器 + 受保护房间） ----
                var pickups = Pickup.List.ToList();
                foreach (var pickup in pickups)
                {
                    if (pickup == null || !pickup.IsSpawned)
                        continue;

                    // 1. 跳过 SCP 物品/特殊武器
                    if (_protectedItemTypes.Contains(pickup.Type))
                    {
                        skippedScp++;
                        continue;
                    }

                    // 2. 额外名称匹配：SCP/GunSCP(SCP武器)/MicroHID/Jailbird/AntiSCP207
                    string typeName = pickup.Type.ToString();
                    if (typeName.StartsWith("SCP") || typeName.StartsWith("GunSCP") ||
                        typeName == "MicroHID" ||
                        typeName == "Jailbird" || typeName == "AntiSCP207")
                    {
                        skippedScp++;
                        continue;
                    }

                    // 3. 跳过受保护房间内的物品
                    try
                    {
                        var room = pickup.Room;
                        if (room != null && (_protectedRoomTypes.Contains(room.Type)
                            || (room.Zone == ZoneType.HeavyContainment && !_protectedRoomTypes.Contains(room.Type))))
                        {
                            skippedRoom++;
                            continue;
                        }
                    }
                    catch { }

                    pickup.Destroy();
                    pickupRemoved++;
                }

                // ---- 清理所有尸体（跳过受保护房间） ----
                var ragdolls = Exiled.API.Features.Ragdoll.List.ToList();
                foreach (var ragdoll in ragdolls)
                {
                    if (ragdoll == null) continue;

                    try
                    {
                        var room = ragdoll.Room;
                        if (room != null && _protectedRoomTypes.Contains(room.Type))
                            continue;
                    }
                    catch { }

                    ragdoll.Destroy();
                    ragdollRemoved++;
                }

                Log.Info($"[clanr] 管理员手动清理：清除 {pickupRemoved} 个掉落物 + {ragdollRemoved} 具尸体 " +
                         $"(跳过SCP/特殊武器: {skippedScp}, 受保护区域: {skippedRoom})");
            }
            catch (Exception ex)
            {
                Log.Error($"[clanr] 手动清理出错: {ex.Message}");
            }
        }
    }
}
