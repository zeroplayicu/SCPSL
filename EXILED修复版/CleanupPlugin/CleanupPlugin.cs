using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Roles;
using MEC;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using CleanupHsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace CleanupPlugin
{
    public class CleanupPlugin : Plugin<CleanupConfig>
    {
        public static CleanupPlugin Instance { get; private set; }

        public override string Name => "CleanupPlugin";
        public override string Author => "Developer";
        public override string Prefix => "cleanup";

        // BUG-03修复: 原实现用 System.Timers.Timer 在后台线程直接操作 Player/Ragdoll/Pickup/Broadcast，
        // 属于跨线程访问 Unity 对象，会随机抛出 "can only be called from the main thread" 或集合修改异常。
        // 现改为 MEC 协程（Unity 安全的定时机制），在主线程执行全部游戏对象操作。
        private CoroutineHandle _cleanupCoroutine;
        private CoroutineHandle _thresholdCoroutine;
        private CoroutineHandle _countdownCoroutine;
        private bool _running;

        // 主线程内单调计时（秒），代替原来依赖 Timer 的倒计时
        private int _lastCleanupCheck;
        private int _lastThresholdCheck;

        private int _countdownRemaining;
        private volatile bool _isCountingDown;

        private HashSet<ItemType> _protectedItemTypes;
        private HashSet<RoomType> _protectedRoomTypes;

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            ParseConfig();

            // BUG-18修复: 对配置值做保护，避免 0/负数导致定时器异常
            int interval = Math.Max(1, Config.CleanupInterval);
            int thresholdInterval = 5;

            _running = true;
            _lastCleanupCheck = 0;
            _lastThresholdCheck = 0;
            _cleanupCoroutine = Timing.RunCoroutine(MainLoop(interval, thresholdInterval));

            Log.Info($"{Name} 加载完成（每{interval}秒自动清理，尸体>{Config.PickupThreshold}提前清扫，清理掉落物:{(Config.CleanPickups ? "开" : "关")}，警告{Config.WarningCountdown}秒）");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            _running = false;
            _isCountingDown = false;
            if (_cleanupCoroutine.IsRunning) Timing.KillCoroutines(_cleanupCoroutine);
            if (_thresholdCoroutine.IsRunning) Timing.KillCoroutines(_thresholdCoroutine);
            if (_countdownCoroutine.IsRunning) Timing.KillCoroutines(_countdownCoroutine);

            Instance = null;
            base.OnDisabled();
        }

        /// <summary>
        /// BUG-03修复: 统一的主线程循环。
        /// 原实现有"每 N 秒清理周期"和"每 5 秒阈值检查"两条独立 Timer 线程，
        /// 现合并为一个 1 秒 tick 的主线程协程，用累计秒数判断何时触发，逻辑等价但线程安全。
        /// </summary>
        private IEnumerator<float> MainLoop(int cleanupInterval, int thresholdInterval)
        {
            int elapsed = 0;
            while (_running)
            {
                yield return Timing.WaitForSeconds(1f);
                if (!_running) break;
                elapsed++;

                try
                {
                    // 阈值检查（每 thresholdInterval 秒）
                    if (elapsed - _lastThresholdCheck >= thresholdInterval)
                    {
                        _lastThresholdCheck = elapsed;
                        OnThresholdCheck();
                    }

                    // 定期清理周期（每 cleanupInterval 秒）
                    if (elapsed - _lastCleanupCheck >= cleanupInterval)
                    {
                        _lastCleanupCheck = elapsed;
                        OnCleanupCycle();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"清理主循环出错: {ex.Message}");
                }
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

        private void OnCleanupCycle()
        {
            try
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

                if (Config.Debug) Log.Debug($"[清理] 当前尸体数量: {ragdollCount}");

                if (ragdollCount > 0)
                {
                    Log.Info($"[清理] 启动清理流程（尸体: {ragdollCount}）");
                    _isCountingDown = true;
                    StartCountdown();
                }
                else
                {
                    if (Config.Debug) Log.Debug("[清理] 无尸体，跳过");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"清理周期出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全获取当前尸体数量。
        /// Ragdoll.List 是游戏实时集合，枚举时可能被同时增删导致
        /// "Collection was modified" 异常，这里先快照成数组并带重试。
        /// 注：BUG-03 修复后本方法已在主线程调用，重试保留作为额外保险。
        /// </summary>
        private int SafeRagdollCount()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    // 先快照成数组，避免在 LINQ 延迟枚举过程中集合被修改
                    var snapshot = Exiled.API.Features.Ragdoll.List.ToArray();
                    return snapshot.Length;
                }
                catch (InvalidOperationException)
                {
                    // 枚举期间集合被修改，短暂等待后重试
                    if (attempt < 2) System.Threading.Thread.Sleep(50);
                }
            }
            return -1; // 多次仍失败，视为暂时无法统计
        }

        private void OnThresholdCheck()
        {
            try
            {
                // 上一轮清理仍在进行则跳过
                if (_isCountingDown) return;

                // 检查游戏是否在运行
                if (Player.List.Count() == 0 || !Round.IsStarted)
                {
                    if (Config.Debug) Log.Debug("[清理] 游戏未开始或无玩家，跳过");
                    return;
                }

                // BUG-20修复: 阈值的意义就是"尸体堆积过快时提前清扫"，
                // 因此不受开局免清扫期限制，否则开局 5 分钟内尸体堆到上限也不会触发。
                int ragdollCount = SafeRagdollCount();
                if (ragdollCount < 0) return; // 统计失败，本 tick 跳过

                if (ragdollCount > Config.PickupThreshold)
                {
                    Log.Info($"[清理] 尸体超过阈值({ragdollCount}>{Config.PickupThreshold})，提前清扫！");
                    _isCountingDown = true;
                    StartCountdown();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"阈值检查出错: {ex.Message}");
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

            // BUG-03修复: 用 MEC 协程替代 Timer，在主线程执行倒计时
            if (_countdownCoroutine.IsRunning) Timing.KillCoroutines(_countdownCoroutine);
            _countdownCoroutine = Timing.RunCoroutine(CountdownLoop());
        }

        /// <summary>BUG-03修复: 主线程倒计时协程</summary>
        private IEnumerator<float> CountdownLoop()
        {
            while (_isCountingDown && _countdownRemaining > 0)
            {
                yield return Timing.WaitForSeconds(1f);
                if (!_isCountingDown) yield break;

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

        // ===== UI-06修复: 清扫提示统一走 HSM =====
        // 原实现每秒 p.Broadcast(Normal) 会让广播进入播放队列：5 秒倒计时会产生
        // 6/5/4/3/2 秒共 5 条广播依次排队，玩家看到倒计时重复约 20 秒、与实际不同步
        // （这正是 BUG-19 移除 ClearBroadcasts 之后引入的副作用）。
        // HSM 按 Id 覆盖上一条，既不进广播队列，也不干扰其他插件的 Broadcast。
        // Y 坐标避让: ac_display=200(管理消息) / anti_tk_alert=400(反组杀通知) /
        //             cleanup_warning=800(清扫) / settle_hint=930(结算)
        private const string CleanupHintId = "cleanup_warning";
        private const int CleanupHintY = 800;

        /// <summary>UI-06修复: 通过 HSM 下发清扫提示（按 Id 覆盖上一条，自动到期销毁）</summary>
        private static void ShowCleanupHint(Player p, string text, float duration)
        {
            if (p == null || p.ReferenceHub == null || !p.IsConnected) return;
            try
            {
                var hint = new CleanupHsmHint
                {
                    Id = CleanupHintId,
                    Text = text,
                    FontSize = 35,
                    YCoordinate = CleanupHintY,
                    Alignment = HintAlignment.Center
                };
                PlayerDisplay.Get(p).ShowHint(hint, duration);
            }
            catch (Exception ex) { Log.Error($"清扫提示显示出错: {ex.Message}"); }
        }

        private void ShowWarning(int seconds)
        {
            try
            {
                // 屏幕文字提示（已取消 CASSIE 语音广播）
                string msg = Config.WarningTemplate.Replace("{time}", seconds.ToString());
                var players = Player.List.ToArray();
                // 覆盖到下一次倒计时 tick，避免服务器卡顿导致提示闪断
                float duration = Math.Max(2, Math.Min(seconds + 1, 60));
                foreach (var p in players)
                {
                    if (p == null) continue;
                    if (p.ReferenceHub == null) continue;
                    if (!p.IsConnected) continue;
                    // UI-06修复: 改用 HSM 下发，不再进广播队列（也不需要 ClearBroadcasts）
                    ShowCleanupHint(p, msg, duration);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"警告提示出错: {ex.Message}");
            }
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

                        // 3. BUG-29修复(明确语义): 收容区域(HCZ)中"未列入保护名单"的区域
                        // （即走廊/过道等过渡空间）的物品跳过清理；HCZ 内已列入
                        // ProtectedRoomTypes 的房间（如 Hcz096/Hcz106 等）不在此分支命中，
                        // 会走正常的销毁流程（其保护由第 2 步处理）。
                        try
                        {
                            var room = pickup.Room;
                            if (room != null && room.Zone == ZoneType.HeavyContainment
                                && !_protectedRoomTypes.Contains(room.Type))
                            {
                                skippedRoom++;
                                if (Config.Debug) Log.Debug($"[清理] 跳过HCZ走廊/非保护区域物品: {pickup.Type} @ {room.Type}");
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
                        // UI-06修复: 改用 HSM 下发，完成提示不再进广播队列、也不会干扰其他插件
                        ShowCleanupHint(p, Config.CleanupDoneMessage, 3f);
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
