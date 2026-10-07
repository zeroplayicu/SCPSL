using System.ComponentModel;
using Exiled.API.Interfaces;

namespace Scp127Fix
{
    public class Scp127FixConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("是否修复 SCP-127 掉落物导致的柜子每帧重试刷屏")]
        public bool FixEnabled { get; set; } = true;

        [Description("Debug模式")]
        public bool Debug { get; set; } = false;
    }
}
