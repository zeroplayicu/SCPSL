namespace ExperiencePlugin
{
    public class ExperienceConfig
    {
        // ===== 经验配置 =====
        public int ExpPerKill { get; set; } = 100;
        public int ExpPerDamage { get; set; } = 1;
        public int DamageSettleDelay { get; set; } = 5;
        public int ExpPerMinute { get; set; } = 10;
        public int ExpPerDeath { get; set; } = 0;
        public string LevelPrefix { get; set; } = "Lv.";
        public int BaseExpPerLevel { get; set; } = 100;
        public bool Debug { get; set; } = false;

        // ===== 显示配置 =====
        public bool ShowStatusAlways { get; set; } = true;
        public int StatusRefreshInterval { get; set; } = 3;
        public int FeedDisplayDuration { get; set; } = 3;
        public bool ShowActiveEffects { get; set; } = true;

        // ===== 模板 =====
        public string StatusMessage { get; set; } =
            "<size=16><color=#FFD700>━━</color> <color=white>{player}</color> " +
            "| <color=#00BFFF>{level}</color> <color=white>{exp}/{maxexp}</color> " +
            "| <color=#FFA500>{kda}</color> " +
            "| <color=#FFA500>{time}</color> <color=#FFD700>━━</color></size>";

        public string DamageFeedMessage { get; set; } =
            "<size=24><color=#FF4444>造成伤害: </color><color=yellow>{xp} xp</color></size>";

        public string KillFeedMessage { get; set; } =
            "<size=22><color=#FF8844>击杀玩家 x{streak}: </color><color=yellow>+{exp} xp</color></size>";

        public string SettleFeedMessage { get; set; } =
            "<size=20><color=#44FF88>结算: 造成 {damage} 伤害 → <color=yellow>+{exp} xp</color></color></size>";

        // ===== 无限备弹 =====
        public bool EnableInfiniteAmmo { get; set; } = true;

        // ===== SCP等级增幅 =====
        public bool EnableScpLevelBuff { get; set; } = true;
        public bool Scp207NoDrain { get; set; } = true;
        public bool Scp500KeepScp207 { get; set; } = true;

        // ===== 人类等级buff =====
        public bool EnableHumanLevelBuff { get; set; } = true;
        public string ClassDItem { get; set; } = "KeycardJanitor";

        // ===== 助攻系统 =====
        public bool EnableAssists { get; set; } = true;
        public int ScpAssistThreshold { get; set; } = 250;
        public int HumanAssistThreshold { get; set; } = 20;
        public int HumanAssistExpPerDamage { get; set; } = 5;
    }
}
