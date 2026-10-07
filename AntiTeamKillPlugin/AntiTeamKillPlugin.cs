using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillPlugin : Plugin<AntiTeamKillConfig>
    {
        public static AntiTeamKillPlugin Instance { get; private set; }
        public AntiTeamKillEventHandler EventHandler { get; private set; }

        private CoroutineHandle _adminMsgCoroutine;

        public override string Name => "AntiTeamKillPlugin";
        public override string Author => "Developer";
        public override string Prefix => "atk";

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new AntiTeamKillEventHandler();

            Exiled.Events.Handlers.Player.Hurting += EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;

            // MEC 协程替代 System.Timers.Timer：主线程执行，避免跨线程摸 Unity API
            _adminMsgCoroutine = Timing.RunCoroutine(AdminMsgRoutine());

            Log.Info($"{Name} v{Version} 加载完成 - 反组杀/警告系统/.AC管理沟通");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_adminMsgCoroutine);

            Exiled.Events.Handlers.Player.Hurting -= EventHandler.OnHurting;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;

            EventHandler?.SaveAllData();
            EventHandler = null;
            Instance = null;

            base.OnDisabled();
        }

        private IEnumerator<float> AdminMsgRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(8f);
                EventHandler?.RefreshAdminDisplay();
            }
        }
    }
}
