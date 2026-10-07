using System.ComponentModel;
using Exiled.API.Interfaces;

namespace SCPplaus
{
    public class SCPplausConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("SCP-106 抓入口袋概率（0-1，如0.75=75%）")]
        public float Scp106PocketChance { get; set; } = 0.75f;

        [Description("SCP-106 未抓到入口袋时造成的伤害")]
        public float Scp106PocketMissDamage { get; set; } = 35f;

        [Description("SCP-096 血量每降低10%额外增加的伤害（初始伤害=Scp096BaseDamage）")]
        public float Scp096DamagePer10Percent { get; set; } = 10f;

        [Description("SCP-096 初始伤害")]
        public float Scp096BaseDamage { get; set; } = 50f;

        [Description("SCP-049 攻击间隔（秒），0=不限制")]
        public float Scp049AttackInterval { get; set; } = 0.8f;

        [Description("SCP-049 普通攻击伤害（秒杀开关 Scp049OneHitKill=false 时生效）")]
        public float Scp049Damage { get; set; } = 40f;

        [Description("SCP-049 普攻秒杀人类")]
        public bool Scp049OneHitKill { get; set; } = false;

        [Description("SCP-049 救人时间（秒）")]
        public float Scp049ResurrectTime { get; set; } = 1f;

        [Description("SCP-939 攻击伤害")]
        public float Scp939Damage { get; set; } = 50f;

        [Description("SCP-3114 攻击伤害")]
        public float Scp3114Damage { get; set; } = 25f;

        // ===== 血量（2026-09-30 用户指定值）=====
        [Description("SCP-173 血量")]
        public int Scp173MaxHp { get; set; } = 6000;

        [Description("SCP-049 血量")]
        public int Scp049MaxHp { get; set; } = 2800;

        [Description("SCP-096 血量")]
        public int Scp096MaxHp { get; set; } = 2000;

        [Description("SCP-106 血量")]
        public int Scp106MaxHp { get; set; } = 2100;

        [Description("SCP-939 血量")]
        public int Scp939MaxHp { get; set; } = 2000;

        [Description("SCP-3114 血量")]
        public int Scp3114MaxHp { get; set; } = 2100;

        [Description("SCP-079 血量（电量上限）")]
        public int Scp079MaxHp { get; set; } = 1500;

        [Description("SCP-049-2 血量")]
        public int Scp0492MaxHp { get; set; } = 750;

        // ===== 护盾 (AHP/人工健康) =====
        [Description("SCP-173 护盾(AHP)")]
        public float Scp173Shield { get; set; } = 1000f;

        [Description("SCP-096 护盾(AHP)")]
        public float Scp096Shield { get; set; } = 800f;

        [Description("SCP-939 护盾(AHP)")]
        public float Scp939Shield { get; set; } = 600f;

        [Description("SCP-3114 护盾(AHP)")]
        public float Scp3114Shield { get; set; } = 500f;

        [Description("SCP-3114 掐喉无冷却（掐完可立即再掐）")]
        public bool Scp3114NoStrangleCooldown { get; set; } = true;

        [Description("修复 SCP-127 掉落物导致的柜子每帧重试刷屏（卡顿）")]
        public bool Scp127CrashFix { get; set; } = true;

        [Description("SCP-939 无限耐力（可持续奔跑不消耗耐力）")]
        public bool Scp939InfiniteStamina { get; set; } = true;

        [Description("Debug模式")]
        public bool Debug { get; set; } = false;
    }
}
