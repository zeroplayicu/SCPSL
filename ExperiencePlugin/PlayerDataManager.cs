using System;
using System.IO;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Exiled.API.Features;

namespace ExperiencePlugin
{
    public class PlayerDataManager
    {
        private readonly ExperiencePlugin _plugin;
        private readonly Dictionary<string, PlayerData> _playerDataCache;
        private readonly string _dataDirectory;
        private readonly string _dataFile;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public PlayerDataManager(ExperiencePlugin plugin)
        {
            _plugin = plugin;
            _playerDataCache = new Dictionary<string, PlayerData>();

            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "ExperienceData");

            _dataFile = Path.Combine(_dataDirectory, "player_data.yml");

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithIndentedSequences()
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
                Log.Info($"已创建数据目录: {_dataDirectory}");
            }

            LoadAllData();

            Log.Info($"已加载 {_playerDataCache.Count} 个玩家的数据");
        }

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
                                _playerDataCache[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载玩家数据失败: {ex.Message}");
                if (_plugin.Config.Debug) Log.Debug($"详细错误: {ex}");
            }
        }

        public void SaveAllData()
        {
            try
            {
                string yaml = _serializer.Serialize(_playerDataCache);
                File.WriteAllText(_dataFile, yaml);
                if (_plugin.Config.Debug)
                    Log.Debug($"已保存 {_playerDataCache.Count} 个玩家的数据到 {_dataFile}");
            }
            catch (Exception ex)
            {
                Log.Error($"保存玩家数据失败: {ex.Message}");
                if (_plugin.Config.Debug) Log.Debug($"详细错误: {ex}");
            }
        }

        public PlayerData GetOrCreatePlayerData(Player player)
        {
            string userId = player.UserId;

            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
            {
                data.PlayerName = player.Nickname;
                data.LastLoginTime = DateTime.Now;
                return data;
            }

            var newData = new PlayerData(userId, player.Nickname);
            _playerDataCache[userId] = newData;

            Log.Info($"新玩家数据已创建: {player.Nickname} (ID: {userId})");

            SaveAllData();
            return newData;
        }

        public PlayerData GetPlayerData(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data : null;
        }

        public bool AddExperience(Player player, int exp)
        {
            var data = GetOrCreatePlayerData(player);
            bool leveledUp = data.AddExperience(exp, _plugin.Config.BaseExpPerLevel);

            if (leveledUp)
                Log.Info($"玩家 {player.Nickname} 升级了！现在是 {data.Level} 级");

            return leveledUp;
        }

        public void UpdatePlayTime(string userId, int minutes)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
                data.TotalPlayTimeMinutes += minutes;
        }

        public void AddKill(string userId)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
                data.TotalKills++;
        }

        public void AddDeath(string userId)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
                data.TotalDeaths++;
        }

        public int GetLevel(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data.Level : 1;
        }

        public int GetExperience(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data.Experience : 0;
        }
    }
}
