using System.ComponentModel;
using Exiled.API.Interfaces;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("攻击队友扣血量")]
        public int TeamHitHpPenalty { get; set; } = 1;
        [Description("攻击队友扣经验")]
        public int TeamHitXpPenalty { get; set; } = 1;
        [Description("击杀队友扣经验")]
        public int TeamKillXpPenalty { get; set; } = 150;
        [Description("自动处罚组杀阈值")]
        public int MaxTeamKillsPerRound { get; set; } = 5;

        [Description("数据文件目录")]
        public string DataDirectory { get; set; } = "AntiTeamKillData";
        [Description("警告数据文件")]
        public string WarningsFile { get; set; } = "warnings.yml";
        [Description("组杀数据文件")]
        public string TeamKillsFile { get; set; } = "teamkills.yml";
    }
}
