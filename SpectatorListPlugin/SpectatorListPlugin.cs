using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;

namespace SpectatorListPlugin
{
    public class SpectatorListPlugin : Plugin<SpectatorListConfig>
    {
        public static SpectatorListPlugin Instance { get; private set; }

        public override string Name => "SpectatorListPlugin";
        public override string Author => "Developer";
        public override string Prefix => "spectatorlist";

        public SpectatorDataManager DataManager { get; private set; }
        public SpectatorSssSettings Sss { get; private set; }
        public SpectatorDisplay Display { get; private set; }
        public SpectatorTracker Tracker { get; private set; }

        private CoroutineHandle _refreshCoroutine;

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            DataManager = new SpectatorDataManager();
            Display = new SpectatorDisplay(this);
            Sss = new SpectatorSssSettings(this);
            Tracker = new SpectatorTracker();

            // 玩家加入时发送 SSS 设置面板（DataManager 会自动创建数据）
            Exiled.Events.Handlers.Player.Verified += OnPlayerVerified;
            // 观战目标切换：维护"被观战者 → 观战者列表"
            Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += Tracker.HandleChangingSpectatedPlayer;

            // 定时刷新观战列表显示（MEC 主线程执行，替代 System.Timers.Timer 线程池回调）
            _refreshCoroutine = Timing.RunCoroutine(RefreshRoutine());

            Log.Info($"{Name} 加载完成 — 观战列表/详细名单 (刷新间隔{Config.RefreshIntervalMs}ms)");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_refreshCoroutine);

            Sss?.RemoveFromAll();
            Display?.ClearAllTexts();
            Tracker?.Clear();
            DataManager?.Shutdown();

            Exiled.Events.Handlers.Player.Verified -= OnPlayerVerified;
            Exiled.Events.Handlers.Player.ChangingSpectatedPlayer -= Tracker.HandleChangingSpectatedPlayer;

            Display = null;
            Sss = null;
            Tracker = null;
            DataManager = null;
            Instance = null;

            base.OnDisabled();
        }

        private IEnumerator<float> RefreshRoutine()
        {
            float interval = Math.Max(0.5f, Config.RefreshIntervalMs / 1000f);
            while (true)
            {
                yield return Timing.WaitForSeconds(interval);
                Tracker.RebuildFromScan();
                Display.RefreshAll();
            }
        }

        private void OnPlayerVerified(VerifiedEventArgs ev)
        {
            if (ev.Player == null) return;
            try
            {
                // 首次创建玩家设置数据并发送 SSS 设置面板
                DataManager.GetOrCreate(ev.Player);
                Sss.SendToPlayer(ev.Player);
            }
            catch (Exception ex)
            {
                Log.Warn($"[观战列表] 玩家加入处理失败: {ex.Message}");
            }
        }
    }
}
