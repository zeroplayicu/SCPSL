using System;
using System.Timers;
using Exiled.API.Features;

namespace ExperiencePlugin
{
    public class ExperiencePlugin : Plugin<ExperienceConfig>
    {
        public override string Name => "ExperiencePlugin";
        public override string Author => "Developer";
        public override string Prefix => "exp";

        public ExperienceEventHandler EventHandler { get; private set; }
        public PlayerDataManager DataManager { get; private set; }

        private Timer _statusTimer;
        private Timer _damageTimer;

        public override void OnEnabled()
        {
            Log.Info($"  {Name} v{Version} 加载中...");

            DataManager = new PlayerDataManager(this);
            EventHandler = new ExperienceEventHandler(this);

            // 注册 EXILED 事件
            Exiled.Events.Handlers.Player.Verified += EventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Dying += EventHandler.OnDying;
            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            Exiled.Events.Handlers.Player.ReloadingWeapon += EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Player.DroppingItem += EventHandler.OnDroppingItem;
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

            Log.Info($"{Name} 加载完成");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            _damageTimer?.Stop();
            _damageTimer?.Dispose();

            DataManager?.SaveAllData();

            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Dying -= EventHandler.OnDying;
            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Hurting -= EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Player.DroppingItem -= EventHandler.OnDroppingItem;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= EventHandler.OnRoundEnded;

            EventHandler = null;
            DataManager = null;

            base.OnDisabled();
        }
    }
}
