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
        [Description("全体聊天前缀")]
        public string BcPrefix { get; set; } = "<color=#FFD700><b>【全服】</b></color>";
        [Description("全体消息显示时长（秒）")]
        public ushort BcDuration { get; set; } = 6;
        [Description("BC命令别名（逗号分隔）")]
        public string BcAliases { get; set; } = "broadcast,all";

        // ===== 团队聊天 (C) =====
        [Description("团队聊天前缀")]
        public string CPrefix { get; set; } = "<color=#00BFFF><b>【团队】</b></color>";
        [Description("团队消息显示时长（秒）")]
        public ushort CDuration { get; set; } = 6;
        [Description("C命令别名（逗号分隔）")]
        public string CAliases { get; set; } = "team,t";

        // ===== 通用 =====
        [Description("消息文字颜色")]
        public string MessageColor { get; set; } = "#E0E0E0";
        [Description("字体大小")]
        public string FontSize { get; set; } = "16";
        [Description("是否记录聊天日志")]
        public bool LogChat { get; set; } = true;
        [Description("是否显示角色徽章（如[NTF][SCP]等）")]
        public bool ShowRoleBadge { get; set; } = true;

        // ===== 阵营颜色 =====
        [Description("MTF/NTF阵营颜色")]
        public string MtfColor { get; set; } = "#5599FF";
        [Description("混沌分裂者颜色")]
        public string ChaosColor { get; set; } = "#55DD55";
        [Description("SCP阵营颜色")]
        public string ScpColor { get; set; } = "#FF5555";
        [Description("科学家颜色")]
        public string ScientistColor { get; set; } = "#FFDD44";
        [Description("D级人员颜色")]
        public string ClassDColor { get; set; } = "#FF9922";
        [Description("设施警卫颜色")]
        public string GuardColor { get; set; } = "#88AACC";
        [Description("其他/未知阵营颜色")]
        public string OtherColor { get; set; } = "#CCCCCC";
    }
}