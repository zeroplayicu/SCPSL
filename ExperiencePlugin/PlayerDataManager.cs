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
        // 定时保存定时器（每60秒异步写入一次，避免连接时阻塞）
        // 下一个可分配的 UID（基于已加载数据最大值+1）
        private int _nextUid = 1;

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

            // 定时保存已移交 ExperiencePlugin 的主线程协程（2026-10-06：原 Timer 回调在线程池，
            // 与主线程写玩家数据存在并发冲突风险）

            Log.Info($"已加载 {_playerDataCache.Count} 个玩家的数据");
        }

        /// <summary>停止定时保存并立即写入（插件卸载时调用）</summary>
        public void Shutdown()
        {
            SaveAllData();
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

                        // 计算下一个可分配 UID（取所有已存在 UID 的最大值 + 1）
                        _nextUid = 1;
                        foreach (var d in _playerDataCache.Values)
                        {
                            if (d.Uid >= _nextUid) _nextUid = d.Uid + 1;
                        }
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
                // 兼容旧数据：已有数据但缺少 UID 时补分配
                if (data.Uid <= 0)
                {
                    data.Uid = _nextUid++;
                    Log.Info($"为已有玩家 {player.Nickname} 分配 UID: {data.Uid}");
                }
                return data;
            }

            var newData = new PlayerData(userId, player.Nickname);
            newData.Uid = _nextUid++;
            _playerDataCache[userId] = newData;

            Log.Info($"新玩家数据已创建: {player.Nickname} (ID: {userId}, UID: {newData.Uid})");

            // 不在此处 SaveAllData() — 同步写文件会阻塞主线程导致连接超时
            // 数据由定时器或回合结束时批量写入
            return newData;
        }

        public PlayerData GetPlayerData(string userId)
        {
            return _playerDataCache.TryGetValue(userId, out PlayerData data) ? data : null;
        }

        /// <summary>根据 UID 查找玩家数据</summary>
        public PlayerData GetPlayerDataByUid(int uid)
        {
            if (uid <= 0) return null;
            foreach (var d in _playerDataCache.Values)
            {
                if (d.Uid == uid) return d;
            }
            return null;
        }

        /// <summary>根据 Steam64ID 查找玩家数据（匹配 UserId 去掉 @steam 后缀）</summary>
        public PlayerData GetPlayerDataBySteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return null;
            steamId = steamId.Trim().ToLowerInvariant();
            foreach (var d in _playerDataCache.Values)
            {
                if (string.IsNullOrEmpty(d.UserId)) continue;
                string uidPart = d.UserId.Split('@')[0].Trim().ToLowerInvariant();
                if (uidPart == steamId) return d;
            }
            return null;
        }

        public bool AddExperience(Player player, int exp)
        {
            var data = GetOrCreatePlayerData(player);
            int finalExp = ApplyVipMultiplier(data, exp);
            bool leveledUp = data.AddExperience(finalExp, _plugin.Config.BaseExpPerLevel);

            if (leveledUp)
                Log.Info($"玩家 {player.Nickname} 升级了！现在是 {data.Level} 级");

            return leveledUp;
        }

        private int ApplyVipMultiplier(PlayerData data, int baseExp)
        {
            // 检查VIP是否过期
            CheckVipExpiry(data);

            if (data.VipLevel <= 0) return baseExp;

            float multiplier = data.VipLevel switch
            {
                2 => _plugin.Config.SvipExpMultiplier,
                _ => _plugin.Config.VipExpMultiplier
            };

            return (int)(baseExp * multiplier);
        }

        /// <summary>应用积分倍率</summary>
        public float ApplyPointsMultiplier(PlayerData data, float basePoints)
        {
            CheckVipExpiry(data);

            if (data.VipLevel <= 0) return basePoints;

            float multiplier = data.VipLevel switch
            {
                2 => _plugin.Config.SvipPointsMultiplier,
                _ => _plugin.Config.VipPointsMultiplier
            };

            return basePoints * multiplier;
        }

        /// <summary>检查并清理过期的VIP</summary>
        private static void CheckVipExpiry(PlayerData data)
        {
            if (data.VipLevel > 0 && data.VipExpiry != DateTime.MinValue && data.VipExpiry != DateTime.MaxValue)
            {
                if (DateTime.Now > data.VipExpiry)
                {
                    data.VipLevel = 0;
                    data.VipExpiry = DateTime.MinValue;
                }
            }
        }

        public void ActivateVip(string userId, int level, int days)
        {
            if (!_playerDataCache.TryGetValue(userId, out var data))
                return;
            data.VipLevel = level;
            data.VipExpiry = days <= 0 ? DateTime.MaxValue : DateTime.Now.AddDays(days);
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

        public float AddPoints(string userId, float points)
        {
            if (_playerDataCache.TryGetValue(userId, out PlayerData data))
            {
                float finalPoints = ApplyPointsMultiplier(data, points);
                data.Points += finalPoints;
                return finalPoints;
            }
            return points;
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
