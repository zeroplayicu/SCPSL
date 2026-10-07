using System.ComponentModel;
using Exiled.API.Interfaces;

namespace YulePlugin
{
    public class YuleConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("是否允许服务器控制台使用 yule 指令")]
        public bool AllowConsole { get; set; } = true;

        [Description("自动教程模式是否默认开启(重启后恢复此值)")]
        public bool AutoTutorialDefaultOn { get; set; } = false;
    }
}
