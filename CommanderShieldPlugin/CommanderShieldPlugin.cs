using System;
using System.Timers;
using Exiled.API.Features;

namespace CommanderShieldPlugin
{
    public class CommanderShieldPlugin : Plugin<CommanderShieldConfig>
    {
        public static CommanderShieldPlugin Instance { get; private set; }
        public ShieldEventHandler EventHandler { get; private set; }

        private Timer _hudTimer;
        private Timer _regenTimer;

        public override string Name => "CommanderShieldPlugin";
        public override string Author => "Developer";
        public override string Prefix => "cshield";

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new ShieldEventHandler();

            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;

            _hudTimer = new Timer(Config.HudRefreshInterval * 1000);
            _hudTimer.Elapsed += (_, _) => EventHandler.RefreshAllHuds();
            _hudTimer.AutoReset = true;
            _hudTimer.Start();

            _regenTimer = new Timer(1000);
            _regenTimer.Elapsed += (_, _) => EventHandler.RegenerateShields();
            _regenTimer.AutoReset = true;
            _regenTimer.Start();

            Log.Info($"{Name} v{Version} 加载完成 - NTF指挥官量子护盾 (AHP:{Config.MaxShieldAHP} HS:{Config.MaxShieldHS})");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            _hudTimer?.Stop(); _hudTimer?.Dispose();
            _regenTimer?.Stop(); _regenTimer?.Dispose();

            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Hurting -= EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;

            EventHandler?.ClearAll();
            EventHandler = null;
            Instance = null;

            base.OnDisabled();
        }
    }
}
