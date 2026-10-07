using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExperiencePlugin
{
    public class WarningManager
    {
        private Dictionary<string, List<string>> _warnings = new Dictionary<string, List<string>>();
        private readonly string _dataDir;
        private readonly string _warningsFile;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public WarningManager()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "ExperienceData");

            _warningsFile = Path.Combine(_dataDir, "warnings.yml");

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);

            LoadAllData();
        }

        public List<string> GetWarnings(string userId)
        {
            return _warnings.TryGetValue(userId, out var list) ? list : new List<string>();
        }

        public int GetTotalWarnings(string userId)
        {
            return _warnings.TryGetValue(userId, out var list) ? list.Count : 0;
        }

        public void AddWarning(string userId, string warning)
        {
            if (!_warnings.ContainsKey(userId))
                _warnings[userId] = new List<string>();
            _warnings[userId].Add($"[{DateTime.Now:yyyy-MM-dd HH:mm}] {warning}");
            SaveWarnings();
        }

        public bool RemoveWarning(string userId, int index)
        {
            if (_warnings.TryGetValue(userId, out var list) && index >= 0 && index < list.Count)
            {
                list.RemoveAt(index);
                if (list.Count == 0) _warnings.Remove(userId);
                SaveWarnings();
                return true;
            }
            return false;
        }

        public List<string> GetAllUserIds()
        {
            return _warnings.Keys.ToList();
        }

        private void LoadAllData()
        {
            try
            {
                if (File.Exists(_warningsFile))
                {
                    string yaml = File.ReadAllText(_warningsFile);
                    if (!string.IsNullOrWhiteSpace(yaml))
                    {
                        var data = _deserializer.Deserialize<Dictionary<string, List<string>>>(yaml);
                        if (data != null) _warnings = data;
                    }
                }
            }
            catch (Exception ex) { Log.Error($"加载警告数据失败: {ex.Message}"); }
        }

        public void SaveAllData() { SaveWarnings(); }

        private void SaveWarnings()
        {
            try
            {
                string yaml = _serializer.Serialize(_warnings);
                File.WriteAllText(_warningsFile, yaml);
            }
            catch (Exception ex) { Log.Error($"保存警告数据失败: {ex.Message}"); }
        }

        /// <summary>检索玩家（支持昵称模糊匹配）</summary>
        public static List<Player> FindPlayers(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<Player>();
            query = query.ToLower();
            return Player.List.Where(p => p != null && !p.IsNPC &&
                (p.Nickname.ToLower().Contains(query) ||
                 p.UserId.ToLower().Contains(query) ||
                 p.Id.ToString() == query)).ToList();
        }
    }
}
