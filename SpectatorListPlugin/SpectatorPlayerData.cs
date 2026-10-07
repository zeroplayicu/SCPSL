using System;

namespace SpectatorListPlugin
{
    /// <summary>单个玩家的观战列表设置</summary>
    [Serializable]
    public class SpectatorPlayerData
    {
        /// <summary>玩家 UserId</summary>
        public string UserId { get; set; }

        /// <summary>玩家名称（冗余存储，便于查看）</summary>
        public string PlayerName { get; set; }

        /// <summary>是否开启观战列表（显示观战人数）</summary>
        public bool ShowSpectatorCount { get; set; } = true;

        /// <summary>是否开启观战列表详细（显示所有人名字）</summary>
        public bool ShowSpectatorDetail { get; set; } = false;

        /// <summary>创建时间</summary>
        public DateTime CreatedTime { get; set; }

        public SpectatorPlayerData() { }

        public SpectatorPlayerData(string userId, string playerName)
        {
            UserId = userId;
            PlayerName = playerName;
            CreatedTime = DateTime.Now;
        }
    }
}
