using System;

namespace ExperiencePlugin
{
    /// <summary>
    /// 玩家数据类 - 存储单个玩家的经验数据
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        /// <summary>
        /// 玩家ID (UserId)
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 玩家名称
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// 当前经验值
        /// </summary>
        public int Experience { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 累计游玩时长（分钟）
        /// </summary>
        public int TotalPlayTimeMinutes { get; set; }

        /// <summary>
        /// 总击杀数
        /// </summary>
        public int TotalKills { get; set; }

        /// <summary>
        /// 总死亡数
        /// </summary>
        public int TotalDeaths { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime LastLoginTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 无参构造函数（用于反序列化）
        /// </summary>
        public PlayerData() { }

        /// <summary>
        /// 创建新玩家数据
        /// </summary>
        public PlayerData(string userId, string playerName)
        {
            UserId = userId;
            PlayerName = playerName;
            Experience = 0;
            Level = 1;
            TotalPlayTimeMinutes = 0;
            TotalKills = 0;
            TotalDeaths = 0;
            LastLoginTime = DateTime.Now;
            CreatedTime = DateTime.Now;
        }

        /// <summary>
        /// 计算升级到下一级所需的经验值
        /// </summary>
        /// <param name="baseExpPerLevel">每级基础经验</param>
        /// <returns>升级所需总经验</returns>
        public int GetExpForNextLevel(int baseExpPerLevel)
        {
            // 公式：基础经验 * 当前等级
            return baseExpPerLevel * Level;
        }

        /// <summary>
        /// 添加经验值，可能触发升级
        /// </summary>
        /// <param name="amount">经验值</param>
        /// <param name="baseExpPerLevel">每级基础经验</param>
        /// <returns>是否升级</returns>
        public bool AddExperience(int amount, int baseExpPerLevel)
        {
            Experience += amount;
            bool leveledUp = false;

            // 检查是否升级
            int expNeeded = GetExpForNextLevel(baseExpPerLevel);
            while (Experience >= expNeeded)
            {
                Experience -= expNeeded;
                Level++;
                leveledUp = true;
                expNeeded = GetExpForNextLevel(baseExpPerLevel);
            }

            return leveledUp;
        }

        /// <summary>
        /// 获取游玩时长格式化字符串
        /// </summary>
        /// <returns>格式化的游玩时长</returns>
        public string GetPlayTimeString()
        {
            int hours = TotalPlayTimeMinutes / 60;
            int minutes = TotalPlayTimeMinutes % 60;

            if (hours > 0)
            {
                return $"{hours}小时{minutes}分钟";
            }
            else
            {
                return $"{minutes}分钟";
            }
        }

        /// <summary>
        /// 获取百分比进度
        /// </summary>
        /// <param name="baseExpPerLevel">每级基础经验</param>
        /// <returns>当前进度百分比</returns>
        public double GetProgressPercentage(int baseExpPerLevel)
        {
            int expNeeded = GetExpForNextLevel(baseExpPerLevel);
            if (expNeeded == 0) return 100;
            return (double)Experience / expNeeded * 100;
        }
    }
}
