using System;
using Exiled.API.Features;

namespace ChatPlugin
{
    public class ChatPlugin : Plugin<ChatConfig>
    {
        public static ChatPlugin Instance { get; private set; }

        public override string Name => "ChatPlugin";
        public override string Author => "Developer";
        public override string Prefix => "chat";

        public override void OnEnabled()
        {
            Instance = this;

            // BUG-10修复: 回合开始时清空聊天缓冲区，避免上一局消息跨局残留/串场
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;

            Log.Info($"{Name} v{Version} 加载完成 - .bc 全体 / .c 团队 / .buff 效果查看 / .info 生涯数据");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            // 编译修复: EXILED 的 Plugin<T> 基类自带 Commands 属性（命令注册表），
            // 会遮蔽 ChatPlugin.Commands 命名空间的解析，必须用 global:: 全限定
            global::ChatPlugin.Commands.ChatMessageBuffer.Clear();
            ClearChatLayer(); // UI-02修复: 卸载时清掉玩家屏幕上的聊天层
            Instance = null;
            base.OnDisabled();
        }

        private void OnRoundStarted()
        {
            global::ChatPlugin.Commands.ChatMessageBuffer.Clear();
            global::ChatPlugin.Commands.InfoCommand.InvalidateCache(); // BUG-17修复: 回合开始时使生涯数据缓存失效
            ClearChatLayer(); // UI-02修复: 新回合清掉上一局残留在屏幕上的聊天堆叠
        }

        /// <summary>UI-02修复: 清空所有玩家屏幕上的聊天 HSM 层</summary>
        private static void ClearChatLayer()
        {
            foreach (var p in Player.List)
            {
                if (p == null || !p.IsConnected) continue;
                global::ChatPlugin.ChatHintDisplay.Clear(p);
            }
        }
    }
}
