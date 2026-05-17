using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Timers;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;

namespace CleanupPlugin
{
    public class CleanupPlugin : Plugin<CleanupConfig>
    {
        public override string Name => "CleanupPlugin";
        public override string Description => "掉落物自动清理插件";
        public override string Author => "Developer";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredApiVersion => new Version(LabApi.Features.LabApiProperties.CompiledVersion);

        private Timer _checkTimer;
        private Timer _countdownTimer;
        private bool _isCountingDown = false;
        private int _countdownRemaining;

        // 缓存反射获取的类型和成员
        private static Type _pickupType;
        private static Type _ragdollType;
        private static PropertyInfo _pickupListProp;
        private static PropertyInfo _ragdollListProp;
        private static PropertyInfo _pickupTypeProp;
        private static PropertyInfo _pickupSpawnedProp;
        private static MethodInfo _pickupDestroyMethod;
        private static MethodInfo _ragdollDestroyMethod;

        private static void EnsureReflectionCache()
        {
            if (_pickupType != null) return;
            _pickupType = Type.GetType("LabApi.Features.Wrappers.Pickups.Pickup, LabApi");
            _ragdollType = Type.GetType("LabApi.Features.Wrappers.Players.Ragdoll, LabApi");
            if (_pickupType != null)
            {
                _pickupListProp = _pickupType.GetProperty("List");
                _pickupTypeProp = _pickupType.GetProperty("Type");
                _pickupSpawnedProp = _pickupType.GetProperty("IsSpawned");
                _pickupDestroyMethod = _pickupType.GetMethod("Destroy");
            }
            if (_ragdollType != null)
            {
                _ragdollListProp = _ragdollType.GetProperty("List");
                _ragdollDestroyMethod = _ragdollType.GetMethod("Destroy");
            }
        }

        public override void Enable()
        {
            EnsureReflectionCache();
            Logger.Info($"  {Name} v{Version} 加载中...");

            _checkTimer = new Timer(Config.CheckInterval * 1000);
            _checkTimer.Elapsed += OnCheckTimer;
            _checkTimer.AutoReset = true;
            _checkTimer.Start();

            Logger.Info($"{Name} 加载完成（每{Config.CheckInterval}秒检测，阈值{Config.CleanupThreshold}个）");
        }

        public override void Disable() { StopTimers(); }

        private void StopTimers()
        {
            _checkTimer?.Stop(); _checkTimer?.Dispose();
            _countdownTimer?.Stop(); _countdownTimer?.Dispose();
            _isCountingDown = false;
        }

        private int GetPickupCount()
        {
            try
            {
                var list = _pickupListProp?.GetValue(null) as ICollection;
                return list?.Count ?? 0;
            }
            catch { return 0; }
        }

        private void OnCheckTimer(object sender, ElapsedEventArgs args)
        {
            try
            {
                if (_isCountingDown) return;
                int count = GetPickupCount();
                if (Config.Debug) Logger.Debug($"[检测] 掉落物: {count}");
                if (count >= Config.CleanupThreshold)
                {
                    Logger.Info($"[清理] 掉落物{count}≥{Config.CleanupThreshold}，启动倒计时");
                    _isCountingDown = true;
                    StartCountdown();
                }
            }
            catch (Exception ex) { Logger.Error($"检测出错: {ex.Message}"); }
        }

        private void StartCountdown()
        {
            _countdownRemaining = Config.CountdownSeconds;
            ShowCountdown();
            _countdownTimer = new Timer(1000);
            _countdownTimer.Elapsed += OnCountdownTick;
            _countdownTimer.AutoReset = true;
            _countdownTimer.Start();
        }

        private void OnCountdownTick(object sender, ElapsedEventArgs args)
        {
            try
            {
                _countdownRemaining--;
                if (_countdownRemaining > 0) { ShowCountdown(); }
                else { _countdownTimer.Stop(); _countdownTimer.Dispose(); ExecuteCleanup(); }
            }
            catch (Exception ex) { Logger.Error($"倒计时出错: {ex.Message}"); _isCountingDown = false; }
        }

        private void ShowCountdown()
        {
            string msg = Config.CountdownTemplate.Replace("{time}", _countdownRemaining.ToString());
            foreach (var p in Player.List) { p.ClearBroadcasts(); p.SendBroadcast(msg, (ushort)2); }
        }

        private void ExecuteCleanup()
        {
            try
            {
                var protectedTypes = Config.ProtectedItemTypes
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).ToHashSet();

                int removed = 0, skipped = 0;
                var pickups = _pickupListProp?.GetValue(null) as IEnumerable;
                if (pickups != null)
                {
                    foreach (var pickup in pickups)
                    {
                        if (pickup == null) continue;
                        if (_pickupSpawnedProp != null && !(bool)_pickupSpawnedProp.GetValue(pickup)) continue;

                        string typeName = ((ItemType)_pickupTypeProp.GetValue(pickup)).ToString().ToLower();
                        if (protectedTypes.Contains(typeName)) { skipped++; continue; }

                        _pickupDestroyMethod?.Invoke(pickup, null);
                        removed++;
                    }
                }

                int ragdollRemoved = 0;
                if (Config.CleanRagdolls)
                {
                    var ragdolls = _ragdollListProp?.GetValue(null) as IEnumerable;
                    if (ragdolls != null)
                    {
                        foreach (var ragdoll in ragdolls)
                        {
                            if (ragdoll == null) continue;
                            _ragdollDestroyMethod?.Invoke(ragdoll, null);
                            ragdollRemoved++;
                        }
                    }
                }

                Logger.Info($"[清理完成] 清除 {removed} 个掉落物 + {ragdollRemoved} 具尸体（跳过{skipped}个SCP物品）");
                foreach (var p in Player.List) { p.ClearBroadcasts(); p.SendBroadcast(Config.CleanupDoneMessage, (ushort)5); }
            }
            catch (Exception ex) { Logger.Error($"清理出错: {ex.Message}"); }
            _isCountingDown = false;
        }
    }
}
