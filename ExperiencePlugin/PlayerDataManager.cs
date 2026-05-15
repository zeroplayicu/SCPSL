using System;
using System.IO;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Exiled.API.Features;

namespace ExperiencePlugin
{
    /// <summary>
    /// 玩家数据管理器 - 负责加载和保存玩家数据
    /// </summary>
    public class PlayerDataManager
    {
        private readonly ExperiencePlugin _plugin;
        private readonly Dictionary<string, PlayerData> _playerDataCache;
        private readonly string _dataDirectory;
        private readonly string _dataFile;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public PlayerDataManager(ExperiencePlugin plugin)
        {
            _plugin = plugin;
            _playerDataCache = new Dictionary<string, PlayerData>();

            // 设置数据存储目录为游戏服务器目录下的EXILED/ExperienceData
            _dataDirectory = Path.Combine(Paths.Exiled, "ExperienceData");
            
            // 数据文件名
            _dataFile = Path.Combine(_dataDirectory, "player_data.yml");

            // 初始化YAML序列化器
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithIndentedSequences()
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            // 创建目录（如果不存在）
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
                Log.Info($"已创建数据目录: {_dataDirectory}");
            }

            // 加载所有玩家数据
            LoadAllData();

            Log.Info($"已加载 {_playerDataCache.Count} 个玩家的数据");
        }

        /// <summary>
        /// 加载所有玩家数据
        /// </summary>
        public void LoadAllData()
        {
            try
            {
                if (File.Exists(_dataFile))
                {
                    string yaml = File.ReadAllText(_dataFile);
                    if (!string.IsNullOrWhiteSpace(yaml))
                    {
                        var data = _deserializer.Deserialize<Dictionary<string, PlayerData>>(yaml);
                        if (data != null)
                        {
                            _playerDataCache.Clear();
                            foreach (var kvp in data)
                            {
                                _playerDataCache[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载玩家数据失败: {ex.Message}");
                if (_plugin.Config.Debug)
                {
                    Log.Debug($"详细错误: {ex}");
                }
            }
        }

        /// <summary>
        /// 保存所有玩家数据
        /// </summary>
        public void SaveAllData()
        {
            try
            {
                string yaml = _serializer.Serialize(_playerDataCache);
                File.WriteAllText(_dataFile, yaml);
                if (_plugin.Config.Debug)
                {
                    Log.Debug($"已保存 {_playerDataCache.Count} 个玩家的数据到 {_dataFile}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"保存玩家数据失败: {ex.Message}");
                if (_plugin.Config.Debug)
                {
                    Log.Debug($"详细错误: {ex}");
                }
            }
        }

        /// <summary>
        /// 获取玩家数据（如果不存在则创建新的）
        /// </summary>
        /// <param name="player">玩家对象</param>
        /// <returns>玩家数据</returns>
        public PlayerData GetOrCreatePlayerData(Player player)
        {
            string userId = player.UserId;

            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
            {
                // 更新玩家名称和最后登录时间
                data.PlayerName = player.Nickname;
                data.LastLoginTime = DateTime.Now;
                return data;
            }

            // 创建新玩家数据
            var newData = new PlayerData(userId, player.Nickname);
            _playerDataCache[userId] = newData;
            
            Log.Info($"新玩家数据已创建: {player.Nickname} (ID: {userId})");

            // 保存新数据
            SaveAllData();

            return newData;
        }

        /// <summary>
        /// 获取玩家数据（如果不存在返回null）
        /// </summary>
        /// <param name="userId">玩家ID</param>
        /// <returns>玩家数据或null</returns>
        public PlayerData GetPlayerData(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data : null;
        }

        /// <summary>
        /// 添加玩家经验
        /// </summary>
        /// <param name="player">玩家对象</param>
        /// <param name="exp">经验值</param>
        /// <returns>是否升级</returns>
        public bool AddExperience(Player player, int exp)
        {
            var data = GetOrCreatePlayerData(player);
            bool leveledUp = data.AddExperience(exp, _plugin.Config.BaseExpPerLevel);

            if (leveledUp)
            {
                Log.Info($"玩家 {player.Nickname} 升级了！现在是 {data.Level} 级");
            }

            return leveledUp;
        }

        /// <summary>
        /// 更新游玩时长
        /// </summary>
        /// <param name="userId">玩家ID</param>
        /// <param name="minutes">分钟数</param>
        public void UpdatePlayTime(string userId, int minutes)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
            {
                data.TotalPlayTimeMinutes += minutes;
            }
        }

        /// <summary>
        /// 增加击杀数
        /// </summary>
        /// <param name="userId">玩家ID</param>
        public void AddKill(string userId)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
            {
                data.TotalKills++;
            }
        }

        /// <summary>
        /// 增加死亡数
        /// </summary>
        /// <param name="userId">玩家ID</param>
        public void AddDeath(string userId)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
            {
                data.TotalDeaths++;
            }
        }

        /// <summary>
        /// 获取玩家等级
        /// </summary>
        public int GetLevel(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data.Level : 1;
        }

        /// <summary>
        /// 获取玩家当前经验
        /// </summary>
        public int GetExperience(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data.Experience : 0;
        }
    }
}
