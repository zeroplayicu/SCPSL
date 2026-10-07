using System.Collections.Generic;
using System.ComponentModel;
using Exiled.API.Interfaces;

namespace FactionPlugin
{
    public class FactionConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        // ========== GOC 阵营（全球超自然联盟）==========
        [Description("启用 GOC 阵营（对局中期刷新 6 名特殊单位）")]
        public bool GocEnabled { get; set; } = true;

        [Description("GOC 刷新时机：回合开始后经过多少秒刷新（对局中期）")]
        public float GocSpawnDelay { get; set; } = 150f;

        [Description("GOC 刷新所需的最少存活人类数量")]
        public int GocMinPlayers { get; set; } = 8;

        // ========== SCP-999 ==========
        [Description("SCP-999光环治疗间隔(秒)")]
        public float Scp999AuraHealInterval { get; set; } = 3f;

        [Description("SCP-999光环每次治疗量")]
        public float Scp999AuraHealAmount { get; set; } = 10f;

        [Description("SCP-999攻击伤害")]
        public float Scp999Damage { get; set; } = 5f;

        // ========== SCP-999 技能 ==========
        [Description("SCP-999技能作用半径")]
        public float Scp999SkillRadius { get; set; } = 10f;

        [Description("技能1(肾上腺素)持续秒数")]
        public float Scp999Skill1Duration { get; set; } = 15f;

        [Description("技能1(肾上腺素)冷却秒数")]
        public float Scp999Skill1Cooldown { get; set; } = 40f;

        [Description("技能2(范围回复)持续秒数")]
        public float Scp999Skill2Duration { get; set; } = 10f;

        [Description("技能2(范围回复)冷却秒数")]
        public float Scp999Skill2Cooldown { get; set; } = 30f;

        [Description("技能2对SCP每秒回复量")]
        public float Scp999Skill2ScpHeal { get; set; } = 50f;

        [Description("技能2对人类每秒回复量")]
        public float Scp999Skill2HumanHeal { get; set; } = 25f;

        // ========== SCP-999 HUD 常驻显示 ==========
        [Description("SCP-999 屏幕中间偏下 HUD 显示的 Y 坐标（HSM 坐标，0=顶部，1080=底部）")]
        public int Scp999HudYCoordinate { get; set; } = 650;

        [Description("SCP-999 屏幕中间偏下 HUD 显示的字体大小")]
        public int Scp999HudFontSize { get; set; } = 18;

        [Description("SCP-999 角色介绍（显示在技能列表顶部）。留空使用默认介绍")]
        public string Scp999RoleIntro { get; set; } = "友好的糖果史莱姆。靠近的友军将持续恢复生命，可释放技能强化治疗/提供增益。";

        [Description("点歌台位置（每项格式 'x,y,z'），每回合开始时自动放置")]
        public List<string> MusicStationPositions { get; set; } = new List<string>
        {
            "38.80,314.11,-28.62"
        };
    }
}
