using System;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace MyFirstPlugin
{
    /// <summary>
    /// 主插件类 - 所有EXILED插件必须继承Plugin&lt;Config&gt;
    /// </summary>
    public class Plugin : Plugin<Config>
    {
        // 插件元数据
        public override string Name => "MyFirstPlugin";
        public override string Author => "Developer";
        public override string Prefix => "myfirstplugin";
        public override Version Version => new Version(1, 0, 0);

        // 事件处理器实例
        public EventHandlers EventHandler { get; private set; }

        /// <summary>
        /// 插件启用时自动调用
        /// </summary>
        public override void OnEnabled()
        {
            Log.Info($"========================================");
            Log.Info($"  {Name} v{Version} 正在加载...");
            Log.Info($"  作者: {Author}");
            Log.Info($"========================================");

            // 初始化事件处理器
            EventHandler = new EventHandlers(this);

            // 注册事件 - 玩家加入事件
            Exiled.Events.Handlers.Player.Verified += EventHandler.OnPlayerVerified;
            
            // 注册事件 - 回合开始事件
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            
            // 注册事件 - 玩家死亡事件
            Exiled.Events.Handlers.Player.Died += EventHandler.OnPlayerDied;

            base.OnEnabled();
            
            Log.Info($"{Name} 插件加载完成！");
        }

        /// <summary>
        /// 插件禁用时自动调用
        /// </summary>
        public override void OnDisabled()
        {
            Log.Info($"{Name} 插件正在卸载...");

            // 注销所有已注册的事件！
            // 这是非常重要的步骤，否则可能导致内存泄漏或错误
            Exiled.Events.Handlers.Player.Verified -= EventHandler.OnPlayerVerified;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnPlayerDied;

            // 清理引用
            EventHandler = null;

            base.OnDisabled();
            
            Log.Info($"{Name} 插件已卸载。");
        }
    }
}
