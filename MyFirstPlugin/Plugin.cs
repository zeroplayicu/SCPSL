using System;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace MyFirstPlugin
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "MyFirstPlugin";
        public override string Author => "Developer";
        public override string Prefix => "myfirstplugin";
        public override Version Version => new Version(1, 0, 0);

        public EventHandlers EventHandler { get; private set; }

        public override void OnEnabled()
        {
            Log.Info($"========================================");
            Log.Info($"  {Name} v{Version} 正在加载...");
            Log.Info($"  作者: {Author}");
            Log.Info($"========================================");

            EventHandler = new EventHandlers(this);

            Exiled.Events.Handlers.Player.Verified += EventHandler.OnPlayerVerified;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnPlayerDied;

            base.OnEnabled();

            Log.Info($"{Name} 插件加载完成！");
        }

        public override void OnDisabled()
        {
            Log.Info($"{Name} 插件正在卸载...");

            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnPlayerVerified;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnPlayerDied;

            EventHandler = null;

            base.OnDisabled();

            Log.Info($"{Name} 插件已卸载。");
        }
    }
}
