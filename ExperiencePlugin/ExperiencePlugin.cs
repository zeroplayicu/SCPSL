using System;
using System.Collections.Generic;
using System.IO;
using MEC;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;

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
        public WarningManager WarningManager { get; private set; }
        public ScpSelectManager ScpSelect { get; private set; }
        public SssSettings Sss { get; private set; }
        public BindService BindManager { get; private set; }
        public HeadTextManager HeadText { get; private set; }

        private CoroutineHandle _displayCoroutine;

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
            WarningManager = new WarningManager();
            ScpSelect = new ScpSelectManager(this);
            Sss = new SssSettings(this);
            BindManager = new BindService(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "ExperienceData"));
            HeadText = new HeadTextManager(this);

            // 注册 EXILED 事件
            Exiled.Events.Handlers.Player.Verified += EventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Dying += EventHandler.OnDying;
            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            Exiled.Events.Handlers.Player.Escaped += EventHandler.OnEscaped;
            Exiled.Events.Handlers.Player.ReloadingWeapon += EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Player.DroppingItem += EventHandler.OnDroppingItem;
            Exiled.Events.Handlers.Player.ItemAdded += EventHandler.OnItemAdded;
            // ===== 远程钥匙卡（背包有卡即可开门/解锁，参考 RemoteKeycard） =====
            Exiled.Events.Handlers.Player.InteractingDoor += EventHandler.OnInteractingDoor;
            Exiled.Events.Handlers.Player.UnlockingGenerator += EventHandler.OnUnlockingGenerator;
            Exiled.Events.Handlers.Player.ActivatingWarheadPanel += EventHandler.OnActivatingWarheadPanel;
            Exiled.Events.Handlers.Player.InteractingLocker += EventHandler.OnInteractingLocker;
            // 回合事件
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStartedScpSelect;
            Exiled.Events.Handlers.Server.RoundEnded += EventHandler.OnRoundEnded;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEndedScpSelect;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEndedSaveData;

            // 统一显示刷新（每0.4秒，减少闪烁）
            // 2026-10-06 修复：原用 System.Timers.Timer（回调在线程池）遍历 Player.List 导致
            // "Operations that change non-concurrent collections must have exclusive access" 报错，
            // 改为 MEC 协程（主线程执行）；60 秒数据保存一并并入此协程。
            _displayCoroutine = Timing.RunCoroutine(DisplayRefreshRoutine());

            Log.Info($"{Name} 加载完成 — 经验/积分/聊天/抽奖/警告/劳改");

            base.OnEnabled();
        }

        /// <summary>主线程协程：每 0.4 秒刷新一次所有玩家显示；每 60 秒保存一次玩家数据</summary>
        private IEnumerator<float> DisplayRefreshRoutine()
        {
            int saveTick = 0;
            while (true)
            {
                yield return Timing.WaitForSeconds(0.4f);
                try { EventHandler.RefreshAllDisplays(); }
                catch (Exception ex) { Log.Error($"显示刷新协程: {ex.Message}"); }

                saveTick++;
                if (saveTick >= 150)   // 0.4 秒 × 150 = 60 秒
                {
                    saveTick = 0;
                    try { DataManager?.SaveAllData(); }
                    catch (Exception ex) { Log.Error($"定时保存: {ex.Message}"); }
                }
            }
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_displayCoroutine);

            Sss?.RemoveFromAll();
            HeadText?.Shutdown();

            DataManager?.Shutdown();
            WarningManager?.SaveAllData();

            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Dying -= EventHandler.OnDying;
            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Player.Escaped -= EventHandler.OnEscaped;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= EventHandler.OnReloadingWeapon;
            Exiled.Events.Handlers.Player.DroppingItem -= EventHandler.OnDroppingItem;
            Exiled.Events.Handlers.Player.ItemAdded -= EventHandler.OnItemAdded;
            // ===== 远程钥匙卡反订阅 =====
            Exiled.Events.Handlers.Player.InteractingDoor -= EventHandler.OnInteractingDoor;
            Exiled.Events.Handlers.Player.UnlockingGenerator -= EventHandler.OnUnlockingGenerator;
            Exiled.Events.Handlers.Player.ActivatingWarheadPanel -= EventHandler.OnActivatingWarheadPanel;
            Exiled.Events.Handlers.Player.InteractingLocker -= EventHandler.OnInteractingLocker;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStartedScpSelect;
            Exiled.Events.Handlers.Server.RoundEnded -= EventHandler.OnRoundEnded;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEndedScpSelect;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEndedSaveData;

            EventHandler = null;
            DataManager = null;
            CdkManager = null;
            Lottery = null;
            WarningManager = null;
            ScpSelect = null;
            Sss = null;
            BindManager = null;
            HeadText = null;
            Instance = null;

            base.OnDisabled();
        }

        private void OnRoundEndedScpSelect(RoundEndedEventArgs ev)
        {
            ScpSelect?.OnRoundEnd();
            HeadText?.OnRoundEnd();
        }

        private void OnRoundStartedScpSelect()
        {
            ScpSelect?.OnRoundStarted();
        }

        /// <summary>回合结束时保存玩家数据（不阻塞主线程）</summary>
        private void OnRoundEndedSaveData(RoundEndedEventArgs ev)
        {
            DataManager?.SaveAllData();
        }
    }
}
