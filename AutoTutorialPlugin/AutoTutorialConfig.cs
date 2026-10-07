using System.ComponentModel;
using Exiled.API.Interfaces;

namespace AutoTutorialPlugin
{
    public class AutoTutorialConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("玩家进入后延迟多少秒才设置角色(确保已完全加载)")]
        public float SetRoleDelay { get; set; } = 0.5f;

        [Description("准备倒计时少于该秒数时，新进入的玩家改为观察者(避免回合马上开始)")]
        public float SpectatorThreshold { get; set; } = 3f;
    }
}
