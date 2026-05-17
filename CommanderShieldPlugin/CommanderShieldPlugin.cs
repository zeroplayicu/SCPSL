using System;
using System.Timers;
using LabApi.Events.CustomHandlers;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;

namespace CommanderShieldPlugin
{
    public class CommanderShieldPlugin : Plugin<CommanderShieldConfig>
    {
        public static CommanderShieldPlugin Instance { get; private set; }
        public ShieldEventHandler EventHandler { get; private set; }

        private Timer _hudTimer;
        private Timer _regenTimer;

        public override string Name => "CommanderShieldPlugin";
        public override string Description => "NTF指挥官量子护盾 - 50AHP + 100HS + 实时HUD";
        public override string Author => "Developer";
        public override Version Version => new Version(2, 0, 0);
        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        public override void Enable()
        {
            Instance = this;
            EventHandler = new ShieldEventHandler();

            CustomHandlersManager.RegisterEventsHandler(EventHandler);
            EventHandler.RegisterRoundEvents();

            // HUD刷新定时器
            _hudTimer = new Timer(Config.HudRefreshInterval * 1000);
            _hudTimer.Elapsed += (_, _) => EventHandler.RefreshAllHuds();
            _hudTimer.AutoReset = true;
            _hudTimer.Start();

            // 护盾再生定时器
            _regenTimer = new Timer(1000);
            _regenTimer.Elapsed += (_, _) => EventHandler.RegenerateShields();
            _regenTimer.AutoReset = true;
            _regenTimer.Start();

            Logger.Info($"{Name} v{Version} 加载完成 - NTF指挥官量子护盾 (AHP:{Config.MaxShieldAHP} HS:{Config.MaxShieldHS})");
        }

        public override void Disable()
        {
            _hudTimer?.Stop();
            _hudTimer?.Dispose();
            _regenTimer?.Stop();
            _regenTimer?.Dispose();

            EventHandler?.UnregisterRoundEvents();
            CustomHandlersManager.UnregisterEventsHandler(EventHandler);

            EventHandler?.ClearAll();
            EventHandler = null;
            Instance = null;
        }
    }
}
