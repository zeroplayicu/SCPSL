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
        /// 玩家唯一UID（正整数，admin系统用于 lv3-6 权限管理）
        /// </summary>
        public int Uid { get; set; }

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
        /// 积分（击杀+0.5，助攻+0.1）
        /// </summary>
        public float Points { get; set; }

        /// <summary>
        /// VIP等级: 0=无, 1=VIP, 2=SVIP
        /// </summary>
        public int VipLevel { get; set; }

        /// <summary>
        /// VIP过期时间 (DateTime.MinValue=未激活, DateTime.MaxValue=永久)
        /// </summary>
        public DateTime VipExpiry { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime LastLoginTime { get; set; }

        /// <summary>
        /// 是否显示VIP/SVIP头衔（玩家可自行开关）
        /// </summary>
        public bool ShowVipTitle { get; set; } = true;

        /// <summary>
        /// 是否劳改中（强制D级、减速、必出硬币）
        /// </summary>
        public bool IsLaborReform { get; set; } = false;

        // ===== SCP自选系统 =====
        /// <summary>今日已使用SCP自选次数</summary>
        public int ScpSelectUsedToday { get; set; }
        /// <summary>SCP自选日期（yyyyMMdd，用于每日重置）</summary>
        public string ScpSelectDate { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 玩家自定义聊天字体颜色（十六进制，如 #FFFFFF #FFD700 #55DD55）
        /// </summary>
        public string ChatColor { get; set; } = "#FFFFFF";

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

            // 防御：每级所需经验必须>0，否则会死循环导致服务器卡死/崩溃（例如击杀SCP给巨额经验时触发）
            if (expNeeded <= 0) return false;

            int safety = 0; // 防止巨额经验导致循环次数过多而卡死
            while (Experience >= expNeeded)
            {
                Experience -= expNeeded;
                Level++;
                leveledUp = true;
                expNeeded = GetExpForNextLevel(baseExpPerLevel);
                if (expNeeded <= 0) break;          // 防御死循环
                if (++safety > 10000) break;        // 极端情况兜底，防止服务器卡死
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
