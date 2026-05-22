using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ChatPlugin
{
    public class ChatConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        // ===== 全体聊天 (BC) =====
        [Description("全体聊天前缀")]
        public string BcPrefix { get; set; } = "<color=#FFD700>[全体]</color>";
        [Description("全体消息显示时长（秒）")]
        public ushort BcDuration { get; set; } = 5;
        [Description("BC命令别名（逗号分隔）")]
        public string BcAliases { get; set; } = "broadcast,all";

        // ===== 团队聊天 (C) =====
        [Description("团队聊天前缀")]
        public string CPrefix { get; set; } = "<color=#00BFFF>[团队]</color>";
        [Description("团队消息显示时长（秒）")]
        public ushort CDuration { get; set; } = 5;
        [Description("C命令别名（逗号分隔）")]
        public string CAliases { get; set; } = "team,t";

        // ===== 通用 =====
        [Description("玩家名颜色")]
        public string PlayerNameColor { get; set; } = "<color=white>";
        [Description("消息颜色")]
        public string MessageColor { get; set; } = "<color=white>";
        [Description("字体大小")]
        public string FontSize { get; set; } = "18";
        [Description("是否记录聊天日志")]
        public bool LogChat { get; set; } = true;
    }
}
