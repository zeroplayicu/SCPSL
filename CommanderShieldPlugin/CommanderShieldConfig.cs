using System.ComponentModel;
using Exiled.API.Interfaces;

namespace CommanderShieldPlugin
{
    public class CommanderShieldConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("护盾AHP最大值")]
        public int MaxShieldAHP { get; set; } = 50;

        [Description("护盾HS最大值")]
        public int MaxShieldHS { get; set; } = 100;

        [Description("HUD刷新间隔（秒）")]
        public float HudRefreshInterval { get; set; } = 1.0f;

        [Description("HUD显示标题")]
        public string HudTitle { get; set; } = "====== 量子护盾 ======";

        [Description("HUD底部装饰")]
        public string HudFooter { get; set; } = "==================";

        [Description("HUD内容模板（{ahp}=当前AHP，{hs}=当前HS）")]
        public string HudContentTemplate { get; set; } = "== AHP:{ahp} HS:{hs} ==";

        [Description("是否替换指挥官卡为O5权限卡")]
        public bool ReplaceCommanderCard { get; set; } = true;
    }
}
