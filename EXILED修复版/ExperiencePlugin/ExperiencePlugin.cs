using System;
using System.IO;
using System.Timers;
using Exiled.API.Features;

namespace ExperiencePlugin
{
    public class ExperiencePlugin : Plugin<ExperienceConfig>
    {
        public static ExperiencePlugin Instance { get; private set; }

        public override string Name => "ExperiencePlugin";
        public override string Author => "Developer";
        public override string Prefix => "exp";

        public ExperienceEventHandler EventHandler { get; private set; }
        public PlayerDataManager DataManager { get; private set; }
        public CdkManager CdkManager { get; private set; }
        public LotteryManager Lottery { get; private set; }

        private Timer _statusTimer;
        private Timer _displayTimer;

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            DataManager = new PlayerDataManager(this);
            EventHandler = new ExperienceEventHandler(this);
            CdkManager = new CdkManager(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "ExperienceData"));
            Lottery = new LotteryManager(Config);

            // 注册 EXILED 事件
            Exiled.Events.Handlers.Player.Verified += EventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Dying += EventHandler.OnDying;
            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.SavingByAntiScp207 += EventHandler.OnSavingByAntiScp207;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            Exiled.Events.Handlers.Player.ReloadingWeapon += EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Player.DroppingItem += EventHandler.OnDroppingItem;
            Exiled.Events.Handlers.Player.ItemAdded += EventHandler.OnItemAdded;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded += EventHandler.OnRoundEnded;

            // 统一显示刷新计时器（每秒）
            _displayTimer = new Timer(1000);
            _displayTimer.Elapsed += (_, _) => EventHandler.RefreshAllDisplays();
            _displayTimer.AutoReset = true;
            _displayTimer.Start();

            if (Config.ShowStatusAlways)
            {
                // 保留独立的状态刷新线程（每3秒完整刷新数据）
                _statusTimer = new Timer(Config.StatusRefreshInterval * 1000);
                _statusTimer.Elapsed += (_, _) => EventHandler.RefreshAllDisplays();
                _statusTimer.AutoReset = true;
                _statusTimer.Start();
            }

            Log.Info($"{Name} 加载完成");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            _displayTimer?.Stop();
            _displayTimer?.Dispose();

            DataManager?.SaveAllData();

            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Dying -= EventHandler.OnDying;
            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.SavingByAntiScp207 -= EventHandler.OnSavingByAntiScp207;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Player.DroppingItem -= EventHandler.OnDroppingItem;
            Exiled.Events.Handlers.Player.ItemAdded -= EventHandler.OnItemAdded;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= EventHandler.OnRoundEnded;

            EventHandler = null;
            DataManager = null;
            CdkManager = null;
            Lottery = null;
            Instance = null;

            base.OnDisabled();
        }
    }
}
