using System.ComponentModel;
using Exiled.API.Interfaces;

namespace CommanderShieldPlugin
{
    public class CommanderShieldConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("AHP最大值")]
        public int MaxShieldAHP { get; set; } = 50;

        [Description("HS最大值")]
        public int MaxShieldHS { get; set; } = 100;

        [Description("每秒回复量")]
        public int RegenPerTick { get; set; } = 1;

        [Description("HUD刷新间隔（秒）")]
        public float HudRefreshInterval { get; set; } = 1.0f;

        [Description("是否替换指挥官卡为O5权限卡")]
        public bool ReplaceCommanderCard { get; set; } = true;
    }
}
