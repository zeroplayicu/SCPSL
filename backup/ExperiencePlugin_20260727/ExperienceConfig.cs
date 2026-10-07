using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ExperiencePlugin
{
    public class ExperienceConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        // ===== 经验配置 =====
        [Description("每次击杀获得的经验值")]
        public int ExpPerKill { get; set; } = 100;
        [Description("每次击杀获得的积分")]
        public float PointsPerKill { get; set; } = 0.5f;
        [Description("每次助攻获得的积分")]
        public float PointsPerAssist { get; set; } = 0.1f;
        [Description("每点伤害获得的经验值")]
        public int ExpPerDamage { get; set; } = 1;
        [Description("伤害结算延迟（秒）")]
        public int DamageSettleDelay { get; set; } = 5;
        [Description("每分钟获得的经验值")]
        public int ExpPerMinute { get; set; } = 10;
        [Description("死亡扣除的经验值")]
        public int ExpPerDeath { get; set; } = 0;
        [Description("等级前缀")]
        public string LevelPrefix { get; set; } = "Lv.";
        [Description("每级基础经验值")]
        public int BaseExpPerLevel { get; set; } = 100;
        [Description("VIP经验倍率")]
        public float VipExpMultiplier { get; set; } = 2.0f;
        [Description("SVIP经验倍率")]
        public float SvipExpMultiplier { get; set; } = 4.0f;
        [Description("VIP积分倍率")]
        public float VipPointsMultiplier { get; set; } = 2.0f;
        [Description("SVIP积分倍率")]
        public float SvipPointsMultiplier { get; set; } = 4.0f;
        [Description("是否启用调试模式")]
        public bool Debug { get; set; } = false;

        // ===== 显示配置 =====
        [Description("是否始终显示状态栏")]
        public bool ShowStatusAlways { get; set; } = true;
        [Description("状态栏刷新间隔（秒）")]
        public int StatusRefreshInterval { get; set; } = 3;
        [Description("战斗反馈显示时长（秒）")]
        public int FeedDisplayDuration { get; set; } = 3;
        [Description("是否显示活跃效果")]
        public bool ShowActiveEffects { get; set; } = true;

        // ===== 模板 =====
        [Description("状态栏消息模板")]
        public string StatusMessage { get; set; } =
            "玩家{vip}{player} 等级{level}[{exp}/{maxexp}] {kda} 积分{points} {time}";

        [Description("VIP徽标模板")]
        public string VipBadge { get; set; } = "<color=#FFD700>【VIP】</color>";
        [Description("SVIP徽标模板")]
        public string SvipBadge { get; set; } = "<color=#FF69B4>【SVIP】</color>";

        [Description("伤害反馈消息模板")]
        public string DamageFeedMessage { get; set; } =
            "<size=14><color=#FF4444>造成伤害: </color><color=yellow>{xp} xp</color></size>";

        [Description("击杀反馈消息模板")]
        public string KillFeedMessage { get; set; } =
            "<size=14><color=#FF8844>击杀玩家 x{streak}: </color><color=yellow>+{exp} xp</color></size>";

        [Description("结算反馈消息模板")]
        public string SettleFeedMessage { get; set; } =
            "<size=20><color=#44FF88>结算: 造成 {damage} 伤害 → <color=yellow>+{exp} xp</color></color></size>";

        // ===== 无限备弹 =====
        [Description("是否启用无限备弹")]
        public bool EnableInfiniteAmmo { get; set; } = true;

        // ===== SCP等级增幅 =====
        [Description("是否启用SCP等级增幅")]
        public bool EnableScpLevelBuff { get; set; } = true;
        [Description("SCP207是否不掉血")]
        public bool Scp207NoDrain { get; set; } = true;
        [Description("SCP500是否不取消SCP207")]
        public bool Scp500KeepScp207 { get; set; } = true;

        // ===== 人类等级buff =====
        [Description("是否启用人类等级Buff")]
        public bool EnableHumanLevelBuff { get; set; } = true;
        [Description("D级人员开局发放的物品类型")]
        public string ClassDItem { get; set; } = "KeycardJanitor";

        // ===== 助攻系统 =====
        [Description("是否启用助攻系统")]
        public bool EnableAssists { get; set; } = true;
        [Description("SCP助攻伤害阈值")]
        public int ScpAssistThreshold { get; set; } = 250;
        [Description("人类助攻伤害阈值")]
        public int HumanAssistThreshold { get; set; } = 20;
        [Description("人类助攻每点伤害经验")]
        public int HumanAssistExpPerDamage { get; set; } = 5;

        // ===== 聊天系统 (BC/C) =====
        [Description("全体聊天前缀")]
        public string BcPrefix { get; set; } = "<color=#FFD700><b>【全服】</b></color>";
        [Description("全体消息显示时长（秒）")]
        public ushort BcDuration { get; set; } = 6;
        [Description("团队聊天前缀")]
        public string CPrefix { get; set; } = "<color=#00BFFF><b>【团队】</b></color>";
        [Description("团队消息显示时长（秒）")]
        public ushort CDuration { get; set; } = 6;
        [Description("消息文字颜色")]
        public string MessageColor { get; set; } = "#E0E0E0";
        [Description("字体大小")]
        public string FontSize { get; set; } = "16";
        [Description("是否记录聊天日志")]
        public bool LogChat { get; set; } = true;
        [Description("是否显示角色徽章")]
        public bool ShowRoleBadge { get; set; } = true;
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

        // ===== 抽奖系统 =====
        [Description("单抽消耗积分")]
        public float LotterySingleCost { get; set; } = 5f;
        [Description("5连抽消耗积分")]
        public float Lottery5Cost { get; set; } = 45f;
        [Description("抽奖确认超时（秒）")]
        public int LotteryConfirmTimeout { get; set; } = 3;
    }
}
