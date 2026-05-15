using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ExperiencePlugin
{
    public class ExperienceConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        // ===== 经验配置 =====
        [Description("击杀经验值")]
        public int ExpPerKill { get; set; } = 100;

        [Description("每1点伤害获得经验")]
        public int ExpPerDamage { get; set; } = 1;

        [Description("伤害结算延迟秒数")]
        public int DamageSettleDelay { get; set; } = 5;

        [Description("每分钟经验")]
        public int ExpPerMinute { get; set; } = 10;

        [Description("死亡扣除经验（0则不扣）")]
        public int ExpPerDeath { get; set; } = 0;

        [Description("等级前缀")]
        public string LevelPrefix { get; set; } = "Lv.";

        [Description("每级基础经验")]
        public int BaseExpPerLevel { get; set; } = 100;

        // ===== 显示配置 =====
        [Description("是否持续显示底部信息")]
        public bool ShowStatusAlways { get; set; } = true;

        [Description("刷新间隔（秒）")]
        public int StatusRefreshInterval { get; set; } = 3;

        [Description("战斗反馈显示时长（秒）")]
        public int FeedDisplayDuration { get; set; } = 3;

        [Description("是否自动在屏幕中显示当前活跃效果（类似EffectDisplay常驻HUD）")]
        public bool ShowActiveEffects { get; set; } = true;

        // ===== 模板 =====
        [Description("底部状态行（{kda}=本局击杀/死亡/助攻）")]
        public string StatusMessage { get; set; } =
            "<size=16><color=#FFD700>━━</color> <color=white>{player}</color> " +
            "| <color=#00BFFF>{level}</color> <color=white>{exp}/{maxexp}</color> " +
            "| <color=#FFA500>{kda}</color> " +
            "| <color=#FFA500>{time}</color> <color=#FFD700>━━</color></size>";

        [Description("伤害反馈")]
        public string DamageFeedMessage { get; set; } =
            "<size=24><color=#FF4444>造成伤害: </color><color=yellow>{xp} xp</color></size>";

        [Description("击杀反馈（{streak}为连杀数，如x1/x2/x3）")]
        public string KillFeedMessage { get; set; } =
            "<size=22><color=#FF8844>击杀玩家 x{streak}: </color><color=yellow>+{exp} xp</color></size>";

        [Description("结算反馈")]
        public string SettleFeedMessage { get; set; } =
            "<size=20><color=#44FF88>结算: 造成 {damage} 伤害 → <color=yellow>+{exp} xp</color></color></size>";

        // ===== 无限备弹 =====
        [Description("是否启用无限备弹")]
        public bool EnableInfiniteAmmo { get; set; } = true;

        // ===== SCP等级增幅 =====
        [Description("SCP等级增幅：25级SCP207×1, 50级SCP207×2, 100级SCP207×3")]
        public bool EnableScpLevelBuff { get; set; } = true;

        [Description("SCP207是否不掉血")]
        public bool Scp207NoDrain { get; set; } = true;

        [Description("SCP500是否不取消SCP207")]
        public bool Scp500KeepScp207 { get; set; } = true;

        // ===== 人类等级buff =====
        [Description("是否启用人类等级buff")]
        public bool EnableHumanLevelBuff { get; set; } = true;

        [Description("D级0级发放物品")]
        public string ClassDItem { get; set; } = "KeycardJanitor";

        // ===== 助攻系统 =====
        [Description("是否启用助攻系统")]
        public bool EnableAssists { get; set; } = true;

        [Description("SCP助攻阈值（对单个SCP造成≥此伤害时助攻）")]
        public int ScpAssistThreshold { get; set; } = 250;

        [Description("人类助攻阈值（对单个人类造成≥此伤害时助攻）")]
        public int HumanAssistThreshold { get; set; } = 20;

        [Description("人类助攻每伤害经验值（1血 = N exp）")]
        public int HumanAssistExpPerDamage { get; set; } = 5;

        // ===== 指挥官护盾 =====
        [Description("是否启用NTF指挥官量子护盾")]
        public bool EnableCommanderShield { get; set; } = true;

        [Description("护盾AHP最大值")]
        public int CommanderMaxAHP { get; set; } = 50;

        [Description("护盾HS最大值")]
        public int CommanderMaxHS { get; set; } = 100;

        [Description("是否替换指挥官卡为O5权限卡")]
        public bool ReplaceCommanderCard { get; set; } = true;

        [Description("护盾HUD文字模板（显示在效果列表下方，{ahp}{maxahp}{hs}{maxhs}）")]
        public string ShieldHudText { get; set; } = "量子护盾 AHP:{ahp}/{maxahp} HS:{hs}/{maxhs}";

        [Description("护盾每1秒再生量")]
        public int ShieldRegenPerTick { get; set; } = 1;
    }
}
