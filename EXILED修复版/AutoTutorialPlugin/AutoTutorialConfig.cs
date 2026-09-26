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

        // 本轮修复: 原 SetRoleDelay 配置项在整个项目中零引用（grep 验证）——
        // BUG-21/22 修复后 OnPlayerVerified 直接设置角色，从不使用延迟。
        // 按 BUG-14 的处理方式移除该"改了不生效"的误导性配置项。

        [Description("准备倒计时少于该秒数时，新进入的玩家改为观察者(避免回合马上开始)")]
        public float SpectatorThreshold { get; set; } = 3f;
    }
}
