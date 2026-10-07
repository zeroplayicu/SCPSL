using System;
using System.IO;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Exiled.API.Features;
using MEC;

namespace SpectatorListPlugin
{
    /// <summary>观战列表玩家设置的数据持久化管理器（YAML）</summary>
    public class SpectatorDataManager
    {
        private readonly Dictionary<string, SpectatorPlayerData> _cache;
        private readonly string _dataFile;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;
        private readonly object _saveLock = new object();
        private CoroutineHandle _saveCoroutine;

        public SpectatorDataManager()
        {
            _cache = new Dictionary<string, SpectatorPlayerData>();

            _dataFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "SpectatorListData", "player_settings.yml");

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithIndentedSequences()
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var dir = Path.GetDirectoryName(_dataFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Log.Info($"[观战列表] 已创建数据目录: {dir}");
            }

            LoadAllData();

            // MEC 主线程定时保存（替代 System.Timers.Timer 线程池回调，
            // 消除"序列化中被修改"与"并发写同一文件"的竞态）
            _saveCoroutine = Timing.RunCoroutine(SaveRoutine());
        }

        public void Shutdown()
        {
            Timing.KillCoroutines(_saveCoroutine);
            SaveAllData();
        }

        private IEnumerator<float> SaveRoutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(60f);
                SaveAllData();
            }
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
                        var data = _deserializer.Deserialize<Dictionary<string, SpectatorPlayerData>>(yaml);
                        if (data != null)
                        {
                            lock (_saveLock)
                            {
                                _cache.Clear();
                                foreach (var kvp in data)
                                    _cache[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[观战列表] 加载玩家设置失败: {ex.Message}");
            }
        }

        public void SaveAllData()
        {
            try
            {
                string yaml;
                lock (_saveLock)
                {
                    yaml = _serializer.Serialize(_cache);
                }
                File.WriteAllText(_dataFile, yaml);
            }
            catch (Exception ex)
            {
                Log.Error($"[观战列表] 保存玩家设置失败: {ex.Message}");
            }
        }

        public SpectatorPlayerData GetOrCreate(Player player)
        {
            string userId = player.UserId;
            if (_cache.TryGetValue(userId, out var data))
            {
                data.PlayerName = player.Nickname;
                return data;
            }

            var newData = new SpectatorPlayerData(userId, player.Nickname);
            _cache[userId] = newData;
            return newData;
        }

        public SpectatorPlayerData Get(string userId)
        {
            return _cache.TryGetValue(userId, out var data) ? data : null;
        }

        /// <summary>设置某个开关的值（type: count=观战人数, detail=详细名单）</summary>
        public void SetToggle(string userId, string type, bool value)
        {
            if (!_cache.TryGetValue(userId, out var data))
                return;
            if (type == "count")
                data.ShowSpectatorCount = value;
            else if (type == "detail")
                data.ShowSpectatorDetail = value;
            SaveAllData();
        }
    }
}
