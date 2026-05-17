using System;
using System.Timers;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Console;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;

namespace ExperiencePlugin
{
    public class ExperiencePlugin : Plugin<ExperienceConfig>
    {
        public override string Name => "ExperiencePlugin";
        public override string Description => "经验等级系统插件";
        public override string Author => "Developer";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        public ExperienceEventHandler EventHandler { get; private set; }
        public PlayerDataManager DataManager { get; private set; }

        private Timer _statusTimer;
        private Timer _damageTimer;

        public override void Enable()
        {
            Logger.Info($"  {Name} v{Version} 加载中...");

            DataManager = new PlayerDataManager(this);
            EventHandler = new ExperienceEventHandler(this);

            CustomHandlersManager.RegisterEventsHandler(EventHandler);
            EventHandler.RegisterRoundEvents();

            if (Config.ShowStatusAlways)
            {
                _statusTimer = new Timer(Config.StatusRefreshInterval * 1000);
                _statusTimer.Elapsed += (_, _) => EventHandler.RefreshAllStatusPanels();
                _statusTimer.AutoReset = true;
                _statusTimer.Start();
            }

            _damageTimer = new Timer(1000);
            _damageTimer.Elapsed += (_, _) => EventHandler.CheckAndSettleDamage();
            _damageTimer.AutoReset = true;
            _damageTimer.Start();

            Logger.Info($"{Name} 加载完成");
        }

        public override void Disable()
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            _damageTimer?.Stop();
            _damageTimer?.Dispose();

            DataManager?.SaveAllData();

            EventHandler?.UnregisterRoundEvents();
            CustomHandlersManager.UnregisterEventsHandler(EventHandler);

            EventHandler = null;
            DataManager = null;
        }
    }
}
