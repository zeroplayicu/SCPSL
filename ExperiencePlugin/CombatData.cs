using System;

namespace ExperiencePlugin
{
    /// <summary>
    /// 战斗数据 - 追踪玩家战斗时的临时数据
    /// </summary>
    public class CombatData
    {
        /// <summary>
        /// 累积的伤害值（用于结算经验）
        /// </summary>
        public int DamageAccumulated { get; set; } = 0;

        /// <summary>
        /// 最后造成伤害的时间
        /// </summary>
        public DateTime LastHitTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 是否正在结算中
        /// </summary>
        public bool IsSettling { get; set; } = false;

        /// <summary>
        /// 当前累积的伤害经验显示值（仅用于UI显示）
        /// </summary>
        public int DisplayDamageXp { get; set; } = 0;

        /// <summary>
        /// 是否有待显示的击杀提示
        /// </summary>
        public bool HasKillExp { get; set; } = false;

        /// <summary>
        /// 当前连杀数（每回合内连续击杀计数，死亡后重置为0）
        /// </summary>
        public int KillStreak { get; set; } = 0;

        /// <summary>
        /// 最后显示战斗反馈的时间
        /// </summary>
        public DateTime LastFeedTime { get; set; } = DateTime.MinValue;
    }
}
