using System;
using System.Timers;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace ExperiencePlugin
{
    public class ExperiencePlugin : Plugin<ExperienceConfig>
    {
        public override string Name => "ExperiencePlugin";
        public override string Author => "Developer";
        public override string Prefix => "exp";
        public override Version Version => new Version(1, 0, 0);

        public EventHandlers EventHandler { get; private set; }
        public PlayerDataManager DataManager { get; private set; }

        private Timer _statusTimer;
        private Timer _damageTimer;

        public override void OnEnabled()
        {
            Log.Info($"  {Name} v{Version} 加载中...");

            DataManager = new PlayerDataManager(this);
            EventHandler = new EventHandlers(this);

            Exiled.Events.Handlers.Player.Verified += EventHandler.OnPlayerVerified;
            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Dying += EventHandler.OnPlayerDying;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnPlayerDied;
            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnPlayerHurt;
            Exiled.Events.Handlers.Player.ReloadingWeapon += EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded += EventHandler.OnRoundEnded;

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

            base.OnEnabled();
            Log.Info($"{Name} 加载完成");
        }

        public override void OnDisabled()
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            _damageTimer?.Stop();
            _damageTimer?.Dispose();

            DataManager?.SaveAllData();

            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnPlayerVerified;
            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnPlayerSpawned;
            Exiled.Events.Handlers.Player.Dying -= EventHandler.OnPlayerDying;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnPlayerDied;
            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnPlayerHurt;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= EventHandler.OnRoundEnded;

            EventHandler = null;
            DataManager = null;

            base.OnDisabled();
        }
    }
}
