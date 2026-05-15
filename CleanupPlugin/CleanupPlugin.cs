using System;
using System.Linq;
using System.Timers;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Interfaces;

namespace CleanupPlugin
{
    public class CleanupPlugin : Plugin<CleanupConfig>
    {
        public override string Name => "CleanupPlugin";
        public override string Author => "Developer";
        public override string Prefix => "cleanup";
        public override Version Version => new Version(1, 0, 0);

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
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _isCountingDown = false;
        }

        /// <summary>
        /// 定时检查掉落物数量
        /// </summary>
        private void OnCheckTimer(object sender, ElapsedEventArgs args)
        {
            try
            {
                if (_isCountingDown) return;

                int count = Pickup.List.Count();
                if (Config.Debug)
                    Log.Debug($"[检测] 掉落物: {count}");

                if (count >= Config.CleanupThreshold)
                {
                    Log.Info($"[清理] 掉落物{count}≥{Config.CleanupThreshold}，启动5秒倒计时");
                    _isCountingDown = true;
                    StartCountdown();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"检测出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动倒计时
        /// </summary>
        private void StartCountdown()
        {
            _countdownRemaining = Config.CountdownSeconds;

            // 立即显示第一帧
            ShowCountdown();

            // 每秒更新一次
            _countdownTimer = new Timer(1000);
            _countdownTimer.Elapsed += OnCountdownTick;
            _countdownTimer.AutoReset = true;
            _countdownTimer.Start();
        }

        /// <summary>
        /// 倒计时每秒回调
        /// </summary>
        private void OnCountdownTick(object sender, ElapsedEventArgs args)
        {
            try
            {
                _countdownRemaining--;

                if (_countdownRemaining > 0)
                {
                    ShowCountdown();
                }
                else
                {
                    // 倒计时结束
                    _countdownTimer.Stop();
                    _countdownTimer.Dispose();
                    ExecuteCleanup();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"倒计时出错: {ex.Message}");
                _isCountingDown = false;
            }
        }

        /// <summary>
        /// 向全服显示倒计时消息
        /// </summary>
        private void ShowCountdown()
        {
            string msg = Config.CountdownTemplate.Replace("{time}", _countdownRemaining.ToString());
            Map.ClearBroadcasts();
            Map.Broadcast(2, msg);
        }

        /// <summary>
        /// 执行清理
        /// </summary>
        private void ExecuteCleanup()
        {
            try
            {
                int removed = 0;
                var pickups = Pickup.List.ToList();
                foreach (var pickup in pickups)
                {
                    if (pickup != null && pickup.IsSpawned)
                    {
                        pickup.Destroy();
                        removed++;
                    }
                }

                Log.Info($"[清理完成] 清除 {removed} 个掉落物");
                Map.ClearBroadcasts();
                Map.Broadcast(5, Config.CleanupDoneMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"清理出错: {ex.Message}");
            }

            _isCountingDown = false;
        }
    }
}
