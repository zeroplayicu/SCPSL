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

        // BUG-14修复: 原 Lv3Permissions / Lv4Permissions / Lv5AllowAll 三个配置项
        // 在整个项目中从未被任何代码引用（权限实际由 AdminManager.GetLevelPermissions 的
        // 硬编码位掩码决定），属于"配置项骗人"——改了配置不生效。
        // 最小改动处理：移除误导性的未使用配置项。若后续需要可配置权限，
        // 应实现为"逗号分隔的权限名 -> 位掩码"解析，并真正接入 GetLevelPermissions。

        [Description("ExperiencePlugin 玩家数据文件路径(默认自动定位到 %AppData%\\EXILED\\ExperienceData\\player_data.yml)")]
        public string ExperienceDataPath { get; set; } = "";
    }
}
