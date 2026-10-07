using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ExperiencePlugin
{
    public class ExperienceConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        // ===== 经验配置 =====
        [Description("每次击杀获得的经验值（人类）")]
        public int ExpPerKill { get; set; } = 100;

        [Description("击杀机动特遣队(MTF)获得的经验")]
        public int ExpPerKillMtf { get; set; } = 100;

        [Description("击杀D级人员(DD)获得的经验")]
        public int ExpPerKillClassD { get; set; } = 100;

        [Description("击杀GOC成员获得的经验")]
        public int ExpPerKillGoc { get; set; } = 350;
        [Description("每次助攻获得的经验值")]
        public int ExpPerAssist { get; set; } = 35;
        [Description("击杀SCP时每1%伤害对应的经验值")]
        public int ScpExpPerPercent { get; set; } = 10;
        [Description("每次击杀获得的积分")]
        public float PointsPerKill { get; set; } = 0.5f;
        [Description("每次助攻获得的积分")]
        public float PointsPerAssist { get; set; } = 0.1f;
        [Description("每分钟获得的经验值")]
        public int ExpPerMinute { get; set; } = 10;
        [Description("死亡扣除的经验值")]
        public int ExpPerDeath { get; set; } = 0;
        [Description("每次成功撤离获得的经验值")]
        public int ExpPerEscape { get; set; } = 1000;
        [Description("每次成功撤离获得的积分")]
        public float PointsPerEscape { get; set; } = 1f;
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
        [Description("击杀特效（屏幕中央显示击杀标记）。默认关闭")]
        public bool EnableKillEffect { get; set; } = false;

        [Description("是否启用调试模式")]
        public bool Debug { get; set; } = false;

        // ===== 显示配置 =====
        [Description("是否始终显示状态栏")]
        public bool ShowStatusAlways { get; set; } = true;
        [Description("状态栏刷新间隔（秒）")]
        public int StatusRefreshInterval { get; set; } = 3;
        [Description("战斗反馈显示时长（秒）")]
        public int FeedDisplayDuration { get; set; } = 6;
        [Description("是否显示活跃效果")]
        public bool ShowActiveEffects { get; set; } = true;
        [Description("是否显示服务器TPS")]
        public bool ShowServerTps { get; set; } = true;

        // ===== 模板 =====
        [Description("状态栏消息模板")]
        public string StatusMessage { get; set; } =
            "玩家{vip}{player} 等级{level}[{exp}/{maxexp}] {kda} 积分{points} {time}";

        [Description("VIP徽标模板")]
        public string VipBadge { get; set; } = "<color=#FFD700>【VIP】</color>";
        [Description("SVIP徽标模板")]
        public string SvipBadge { get; set; } = "<color=#FF69B4>【SVIP】</color>";

        [Description("击杀反馈消息模板(去掉 <size> 内联标签，由专用层 FontSize 控制；<color> 标签可正常解析)")]
        public string KillFeedMessage { get; set; } =
            "<color=#FF8844>击杀 x{streak}: </color><color=yellow>+{exp} xp</color>";

        // ===== 无限备弹 =====
        [Description("是否启用无限备弹")]
        public bool EnableInfiniteAmmo { get; set; } = true;
        [Description("手持枪械时背包强制锁定的子弹数量")]
        public int InfiniteAmmoCount { get; set; } = 120;

        // ===== SCP等级增幅 =====
        [Description("是否启用SCP等级增幅")]
        public bool EnableScpLevelBuff { get; set; } = true;
        [Description("SCP500是否不取消SCP207")]
        public bool Scp500KeepScp207 { get; set; } = true;

        // ===== 人类等级buff =====
        [Description("是否启用人类等级Buff")]
        public bool EnableHumanLevelBuff { get; set; } = true;
        [Description("D级人员开局发放的物品类型")]
        public string ClassDItem { get; set; } = "KeycardJanitor";
        [Description("保安/博士(非D级)出生时等级超过此值给1瓶可乐(SCP207)")]
        public int SpawnColaLevel { get; set; } = 50;

        // ===== 助攻系统 =====
        [Description("是否启用助攻系统")]
        public bool EnableAssists { get; set; } = true;
        [Description("助攻最小伤害阈值（低于此伤害不计算助攻）")]
        public int AssistMinDamage { get; set; } = 1;

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
        public string ClassDColor { get; set; } = "#FFD700";
        [Description("设施警卫颜色")]
        public string GuardColor { get; set; } = "#888888";
        [Description("其他/未知阵营颜色")]
        public string OtherColor { get; set; } = "#CCCCCC";

        // ===== 抽奖系统 =====
        [Description("单抽消耗积分")]
        public float LotterySingleCost { get; set; } = 5f;
        [Description("5连抽消耗积分")]
        public float Lottery5Cost { get; set; } = 45f;
        [Description("抽奖确认超时（秒）")]
        public int LotteryConfirmTimeout { get; set; } = 30;

        // ===== 劳改系统 =====
        [Description("劳改时移动速度减少百分比(0-100)")]
        public int LaborReformSpeedReduction { get; set; } = 20;
        [Description("劳改时发放硬币数量")]
        public int LaborReformCoinCount { get; set; } = 3;
        [Description("劳改头衔前缀")]
        public string LaborReformTitle { get; set; } = "<color=#FF4444>劳改中</color>";
        [Description("劳改头衔颜色")]
        public string LaborReformColor { get; set; } = "#FF4444";

        // ===== SCP自选系统 =====
        [Description("SCP自选每5级增加一次机会（0级=1次, 5级=3次, 10级=5次...）")]
        public int ScpSelectBaseCount { get; set; } = 1;
        [Description("SCP自选VIP额外次数")]
        public int ScpSelectVipBonus { get; set; } = 10;
        [Description("SCP自选SVIP额外次数")]
        public int ScpSelectSvipBonus { get; set; } = 35;
        [Description("允许自选的SCP角色列表（分号分隔）")]
        public string ScpSelectRoles { get; set; } = "Scp049;Scp096;Scp173;Scp939;Scp106;Scp079";

        // ===== 远程钥匙卡系统（参考 RemoteKeycard） =====
        [Description("是否启用远程钥匙卡（背包里有卡就能开门/解锁，无需手持）")]
        public bool RemoteKeycardEnabled { get; set; } = true;
        [Description("远程钥匙卡是否作用于门")]
        public bool RemoteKeycardAffectDoors { get; set; } = true;
        [Description("远程钥匙卡是否作用于发电机")]
        public bool RemoteKeycardAffectGenerators { get; set; } = true;
        [Description("远程钥匙卡是否作用于核弹面板")]
        public bool RemoteKeycardAffectWarheadPanel { get; set; } = true;
        [Description("远程钥匙卡是否作用于SCP储物柜")]
        public bool RemoteKeycardAffectScpLockers { get; set; } = true;
        [Description("失忆症（SCP-008）是否影响远程钥匙卡使用")]
        public bool RemoteKeycardAmnesiaMatters { get; set; } = true;
        [Description("一次性钥匙卡使用后是否销毁")]
        public bool RemoteKeycardSingleUseDestroy { get; set; } = true;
    }
}
