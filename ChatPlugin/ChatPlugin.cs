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
            Log.Info($"{Name} v{Version} 加载完成 - .bc 全体 / .c 团队 / .buff 效果查看 / .info 生涯数据");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Instance = null;
            base.OnDisabled();
        }
    }
}
