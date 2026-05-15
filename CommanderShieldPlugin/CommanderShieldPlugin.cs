using System;
using System.Timers;
using Exiled.API.Features;

namespace CommanderShieldPlugin
{
    public class CommanderShieldPlugin : Plugin<CommanderShieldConfig>
    {
        public static CommanderShieldPlugin Instance { get; private set; }
        public EventHandlers EventHandler { get; private set; }

        private Timer _hudTimer;

        public override string Name => "CommanderShieldPlugin";
        public override string Author => "Developer";
        public override string Prefix => "cmdshield";
        public override Version Version => new Version(1, 0, 0);

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new EventHandlers();

            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnPlayerHurting;

            _hudTimer = new Timer(Config.HudRefreshInterval * 1000);
            _hudTimer.Elapsed += (_, _) => EventHandler.RefreshAllHuds();
            _hudTimer.AutoReset = true;
            _hudTimer.Start();

            base.OnEnabled();
            Log.Info($"{Name} v{Version} 加载完成 - NTF指挥官量子护盾");
        }

        public override void OnDisabled()
        {
            _hudTimer?.Stop();
            _hudTimer?.Dispose();

            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Hurting -= EventHandler.OnPlayerHurting;

            EventHandler?.ClearAll();
            EventHandler = null;
            Instance = null;

            base.OnDisabled();
        }
    }
}
