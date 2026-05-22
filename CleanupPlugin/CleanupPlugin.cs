using System;
using System.Linq;
using System.Timers;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Items;
using InventorySystem.Items.Pickups;
using PlayerRoles.Ragdolls;

namespace CleanupPlugin
{
    public class CleanupPlugin : Plugin<CleanupConfig>
    {
        public override string Name => "CleanupPlugin";
        public override string Author => "Developer";
        public override string Prefix => "cleanup";

        private Timer _checkTimer;
        private Timer _countdownTimer;
        private bool _isCountingDown = false;
        private int _countdownRemaining;

        public override void OnEnabled()
        {
            Log.Info($"  {Name} v{Version} 加载中...");

            _checkTimer = new Timer(Config.CheckInterval * 1000);
            _checkTimer.Elapsed += OnCheckTimer;
            _checkTimer.AutoReset = true;
            _checkTimer.Start();

            Log.Info($"{Name} 加载完成（每{Config.CheckInterval}秒检测，阈值{Config.CleanupThreshold}个）");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            StopTimers();
            base.OnDisabled();
        }

        private void StopTimers()
        {
            _checkTimer?.Stop(); _checkTimer?.Dispose();
            _countdownTimer?.Stop(); _countdownTimer?.Dispose();
            _isCountingDown = false;
        }

        private int GetPickupCount()
        {
            try { return Pickup.List?.Count() ?? 0; }
            catch { return 0; }
        }

        private void OnCheckTimer(object sender, ElapsedEventArgs args)
        {
            try
            {
                if (_isCountingDown) return;
                int count = GetPickupCount();
                if (Config.Debug) Log.Debug($"[检测] 掉落物: {count}");
                if (count >= Config.CleanupThreshold)
                {
                    Log.Info($"[清理] 掉落物{count}≥{Config.CleanupThreshold}，启动倒计时");
                    _isCountingDown = true;
                    StartCountdown();
                }
            }
            catch (Exception ex) { Log.Error($"检测出错: {ex.Message}"); }
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
            catch (Exception ex) { Log.Error($"倒计时出错: {ex.Message}"); _isCountingDown = false; }
        }

        private void ShowCountdown()
        {
            string msg = Config.CountdownTemplate.Replace("{time}", _countdownRemaining.ToString());
            foreach (var p in Player.List)
            {
                if (p == null) continue;
                p.ClearBroadcasts();
                p.Broadcast(2, msg, Broadcast.BroadcastFlags.Normal);
            }
        }

        private void ExecuteCleanup()
        {
            try
            {
                var protectedTypes = Config.ProtectedItemTypes
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).ToHashSet();

                int removed = 0, skipped = 0;

                // 清理掉落物
                foreach (var pickup in Pickup.List.ToList())
                {
                    if (pickup == null || !pickup.IsSpawned) continue;
                    string typeName = pickup.Type.ToString().ToLower();
                    if (protectedTypes.Contains(typeName)) { skipped++; continue; }
                    pickup.Destroy();
                    removed++;
                }

                // 清理尸体
                int ragdollRemoved = 0;
                if (Config.CleanRagdolls)
                {
                    foreach (var ragdoll in Exiled.API.Features.Ragdoll.List.ToList())
                    {
                        if (ragdoll == null) continue;
                        ragdoll.Delete();
                        ragdollRemoved++;
                    }
                }

                Log.Info($"[清理完成] 清除 {removed} 个掉落物 + {ragdollRemoved} 具尸体（跳过{skipped}个SCP物品）");
                foreach (var p in Player.List)
                {
                    if (p == null) continue;
                    p.ClearBroadcasts();
                    p.Broadcast(5, Config.CleanupDoneMessage, Broadcast.BroadcastFlags.Normal);
                }
            }
            catch (Exception ex) { Log.Error($"清理出错: {ex.Message}"); }
            _isCountingDown = false;
        }
    }
}
