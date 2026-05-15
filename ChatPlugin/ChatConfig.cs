using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ChatPlugin
{
    public class ChatConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        // ===== 全体聊天 (BC) =====
        [Description("BC 全体聊天前缀")]
        public string BcPrefix { get; set; } = "<color=#FFD700>[全体]</color>";

        [Description("BC 消息显示时长（秒），到期自动消除")]
        public ushort BcDuration { get; set; } = 5;

        [Description("BC 命令别名（逗号分隔）")]
        public string BcAliases { get; set; } = "broadcast,all";

        // ===== 团队聊天 (C) =====
        [Description("C 团队聊天前缀")]
        public string CPrefix { get; set; } = "<color=#00BFFF>[团队]</color>";

        [Description("C 消息显示时长（秒），到期自动消除")]
        public ushort CDuration { get; set; } = 5;

        [Description("C 命令别名（逗号分隔）")]
        public string CAliases { get; set; } = "team,t";

        // ===== 通用 =====
        [Description("玩家名颜色（含开标签）")]
        public string PlayerNameColor { get; set; } = "<color=white>";

        [Description("消息颜色（含开标签）")]
        public string MessageColor { get; set; } = "<color=white>";

        [Description("字体大小")]
        public string FontSize { get; set; } = "18";

        [Description("是否在服务器日志中记录聊天")]
        public bool LogChat { get; set; } = true;
    }
}
