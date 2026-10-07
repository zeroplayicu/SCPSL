using System;

namespace ExperiencePlugin
{
    /// <summary>
    /// 战斗数据 - 追踪玩家战斗时的临时数据
    /// </summary>
    public class CombatData
    {
        /// <summary>
        /// 当前连杀数（每回合内连续击杀计数，死亡后重置为0）
        /// </summary>
        public int KillStreak { get; set; } = 0;

        /// <summary>
        /// 是否有待显示的击杀提示
        /// </summary>
        public bool HasKillExp { get; set; } = false;

        /// <summary>
        /// 本次击杀实际获得的经验值（用于显示，SCP击杀经验不固定）
        /// </summary>
        public int KillExpAwarded { get; set; } = 0;

        /// <summary>
        /// 最后显示战斗反馈的时间
        /// </summary>
        public DateTime LastFeedTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 待显示的惩罚经验（攻击队友扣XP显示）
        /// </summary>
        public int DisplayPenaltyXp { get; set; } = 0;

        /// <summary>
        /// 待显示的结算/升级消息文本（纯文本，不含HTML标签，由专用层渲染避免标签被错误转大写显示为字面量）
        /// </summary>
        public string SettlePendingText { get; set; } = "";

        /// <summary>
        /// 结算/升级消息显示用的颜色（HEX 或 lime/yellow 等）
        /// </summary>
        public string SettlePendingColor { get; set; } = "white";

        /// <summary>
        /// 结算/升级消息显示到期时间
        /// </summary>
        public DateTime SettlePendingTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 待显示的积分通知消息（击杀/助攻获得积分，右侧显示）
        /// </summary>
        public string PointsNotifyText { get; set; } = "";

        /// <summary>
        /// 积分通知显示到期时间
        /// </summary>
        public DateTime PointsNotifyTime { get; set; } = DateTime.MinValue;
    }
}
