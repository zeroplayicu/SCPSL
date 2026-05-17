using System;
using LabApi.Events.CustomHandlers;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillPlugin : Plugin<AntiTeamKillConfig>
    {
        public static AntiTeamKillPlugin Instance { get; private set; }
        public AntiTeamKillEventHandler EventHandler { get; private set; }

        public override string Name => "AntiTeamKillPlugin";
        public override string Description => "反恶意组杀 + 警告系统 + 玩家管理沟通";
        public override string Author => "Developer";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        public override void Enable()
        {
            Instance = this;
            EventHandler = new AntiTeamKillEventHandler();

            CustomHandlersManager.RegisterEventsHandler(EventHandler);
            EventHandler.RegisterRoundEvents();

            Logger.Info($"{Name} v{Version} 加载完成 - 反组杀/警告系统/.AC管理沟通");
        }

        public override void Disable()
        {
            EventHandler?.UnregisterRoundEvents();
            CustomHandlersManager.UnregisterEventsHandler(EventHandler);
            EventHandler?.SaveAllData();
            EventHandler = null;
            Instance = null;
        }
    }
}
