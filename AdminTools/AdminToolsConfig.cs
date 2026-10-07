using System.ComponentModel;
using Exiled.API.Interfaces;

namespace AdminTools
{
    public class AdminToolsConfig : IConfig
    {
        [Description("是否启用本插件")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试日志")]
        public bool Debug { get; set; } = false;

        [Description("允许执行 admin 命令的最低等级(默认5，即仅 高级admin/服主 可用)")]
        public int MinManageLevel { get; set; } = 5;

        [Description("是否允许从服务器控制台(终端)使用 admin 指令(默认true)")]
        public bool AllowConsole { get; set; } = true;

        [Description("lv3(见习admin)可用的指令权限(逗号分隔)。变教程/传送/改角色等")]
        public string Lv3Permissions { get; set; } = "tutorial,tp,teleport,role,player,effect,size,scale";

        [Description("lv4(普通admin)可用的指令权限(逗号分隔)。lv3 + 刷物等")]
        public string Lv4Permissions { get; set; } = "tutorial,tp,teleport,role,player,effect,size,scale,item,give,spawn,drop";

        [Description("lv5/lv6(高级admin/服主)是否允许所有指令(默认true, 等价 * 通配)")]
        public bool Lv5AllowAll { get; set; } = true;

        [Description("ExperiencePlugin 玩家数据文件路径(默认自动定位到 %AppData%\\EXILED\\ExperienceData\\player_data.yml)")]
        public string ExperienceDataPath { get; set; } = "";
    }
}
