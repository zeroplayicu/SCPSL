namespace AntiTeamKillPlugin
{
    public class AntiTeamKillConfig
    {
        public bool Debug { get; set; } = false;

        // 队友惩罚
        public int TeamHitHpPenalty { get; set; } = 1;        // 攻击队友扣血量
        public int TeamHitXpPenalty { get; set; } = 1;        // 攻击队友扣经验
        public int TeamKillXpPenalty { get; set; } = 150;      // 击杀队友扣经验
        public int MaxTeamKillsPerRound { get; set; } = 5;     // 自动处罚阈值

        // 数据文件路径
        public string DataDirectory { get; set; } = "AntiTeamKillData";
        public string WarningsFile { get; set; } = "warnings.yml";
        public string TeamKillsFile { get; set; } = "teamkills.yml";
    }
}
