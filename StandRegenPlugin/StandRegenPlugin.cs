using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace StandRegenPlugin
{
    public class StandRegenPlugin : Plugin<StandRegenConfig>
    {
        public static StandRegenPlugin Instance { get; private set; }

        public override string Name => "StandRegenPlugin";
        public override string Author => "Developer";
        public override string Prefix => "standregen";

        public StandRegenEventHandler EventHandler { get; private set; }

        private CoroutineHandle _tickCoroutine;

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new StandRegenEventHandler(this);

            // 定时检测站立回血（MEC 主线程执行，替代 System.Timers.Timer 线程池回调）
            _tickCoroutine = Timing.RunCoroutine(TickRoutine());

            Log.Info($"{Name} 加载完成 — SCP站立回血 (站立{Config.StandDelaySeconds}秒后每秒+{Config.RegenPerSecond}血)");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_tickCoroutine);

            EventHandler?.Clear();
            EventHandler = null;
            Instance = null;
            base.OnDisabled();
        }

        private IEnumerator<float> TickRoutine()
        {
            float interval = Math.Max(0.25f, Config.RefreshIntervalMs / 1000f);
            while (true)
            {
                yield return Timing.WaitForSeconds(interval);
                EventHandler?.Tick();
            }
        }
    }
}
