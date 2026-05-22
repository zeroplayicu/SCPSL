using System;
using System.Timers;
using Exiled.API.Features;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillPlugin : Plugin<AntiTeamKillConfig>
    {
        public static AntiTeamKillPlugin Instance { get; private set; }
        public AntiTeamKillEventHandler EventHandler { get; private set; }

        private Timer _adminMsgTimer;

        public override string Name => "AntiTeamKillPlugin";
        public override string Author => "Developer";
        public override string Prefix => "atk";

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new AntiTeamKillEventHandler();

            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;

            _adminMsgTimer = new Timer(8000);
            _adminMsgTimer.Elapsed += (_, _) => EventHandler.ShowNextAdminMessage();
            _adminMsgTimer.AutoReset = true;
            _adminMsgTimer.Start();

            Log.Info($"{Name} v{Version} 加载完成 - 反组杀/警告系统/.AC管理沟通");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            _adminMsgTimer?.Stop();
            _adminMsgTimer?.Dispose();

            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;

            EventHandler?.SaveAllData();
            EventHandler = null;
            Instance = null;

            base.OnDisabled();
        }
    }
}
