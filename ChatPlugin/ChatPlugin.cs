using System;
using LabApi.Features.Console;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;

namespace ChatPlugin
{
    public class ChatPlugin : Plugin<ChatConfig>
    {
        public static ChatPlugin Instance { get; private set; }

        public override string Name => "ChatPlugin";
        public override string Description => "全体聊天(BC) + 团队聊天(C) 插件";
        public override string Author => "Developer";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        public override void Enable()
        {
            Instance = this;
            Logger.Info($"{Name} v{Version} 加载完成 - .bc 全体 / .c 团队 / .buff 效果查看 / .info 生涯数据");
        }

        public override void Disable()
        {
            Instance = null;
        }
    }
}
