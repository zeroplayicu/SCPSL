using System.ComponentModel;
using Exiled.API.Interfaces;

namespace MyFirstPlugin
{
    /// <summary>
    /// 配置类 - 定义插件的所有可配置项
    /// 必须实现IConfig接口
    /// </summary>
    public class Config : IConfig
    {
        /// <summary>
        /// 是否启用插件
        /// IsEnabled是必须的！EXILED用它来判断插件是否启用
        /// </summary>
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 是否启用调试模式
        /// 调试模式会输出更多日志信息
        /// </summary>
        [Description("是否启用调试模式")]
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// 玩家加入时显示的欢迎消息
        /// 支持颜色代码，例如: &lt;color=red&gt;红色文字&lt;/color&gt;
        /// </summary>
        [Description("玩家加入时显示的欢迎消息")]
        public string WelcomeMessage { get; set; } = "欢迎来到服务器！祝您游戏愉快！";

        /// <summary>
        /// 欢迎消息显示时长（秒）
        /// </summary>
        [Description("欢迎消息显示时长（秒）")]
        public ushort WelcomeDuration { get; set; } = 5;

        /// <summary>
        /// 死亡时显示的消息
        /// </summary>
        [Description("死亡时显示的消息")]
        public string DeathMessage { get; set; } = "您已死亡！";

        /// <summary>
        /// 是否在回合开始时广播消息
        /// </summary>
        [Description("是否在回合开始时广播消息")]
        public bool BroadcastOnRoundStart { get; set; } = true;

        /// <summary>
        /// 回合开始广播消息
        /// </summary>
        [Description("回合开始时的广播消息")]
        public string RoundStartMessage { get; set; } = "新回合已开始！";

        /// <summary>
        /// 回合开始广播时长
        /// </summary>
        [Description("广播消息显示时长（秒）")]
        public ushort RoundStartDuration { get; set; } = 3;
    }
}
