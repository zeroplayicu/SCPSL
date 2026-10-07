using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExperiencePlugin
{
    public class CdkManager
    {
        private readonly string _dataFile;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;
        private readonly Random _rng = new Random();

        public List<CdkEntry> Codes { get; private set; } = new List<CdkEntry>();

        public class CdkEntry
        {
            public string Code { get; set; }
            public string Type { get; set; } // "VIP" or "SVIP"
            public int Days { get; set; }
            public bool Used { get; set; }
            public string UsedBy { get; set; }
            public DateTime UsedAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public CdkManager(string dataDir)
        {
            _dataFile = Path.Combine(dataDir, "cdk_data.yml");
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_dataFile))
                {
                    var yaml = File.ReadAllText(_dataFile);
                    if (!string.IsNullOrWhiteSpace(yaml))
                        Codes = _deserializer.Deserialize<List<CdkEntry>>(yaml) ?? new List<CdkEntry>();
                }
            }
            catch (Exception ex) { Log.Error($"加载CDK失败: {ex.Message}"); }
        }

        public void Save()
        {
            try
            {
                var yaml = _serializer.Serialize(Codes);
                File.WriteAllText(_dataFile, yaml);
            }
            catch (Exception ex) { Log.Error($"保存CDK失败: {ex.Message}"); }
        }

        public List<string> Generate(int count, string type, int days)
        {
            var codes = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string code = GenerateCode();
                Codes.Add(new CdkEntry
                {
                    Code = code,
                    Type = type.ToUpper(),
                    Days = days,
                    Used = false,
                    CreatedAt = DateTime.Now
                });
                codes.Add(code);
            }
            Save();
            return codes;
        }

        public CdkEntry Redeem(string code, Player player)
        {
            var entry = Codes.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && !c.Used);
            if (entry == null) return null;

            entry.Used = true;
            entry.UsedBy = player.UserId;
            entry.UsedAt = DateTime.Now;

            int level = entry.Type == "SVIP" ? 2 : 1;
            ExperiencePlugin.Instance.DataManager.ActivateVip(player.UserId, level, entry.Days);

            Save();
            return entry;
        }

        private string GenerateCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[14]; // XXXX-XXXX-XXXX = 14字符
            for (int i = 0; i < 14; i++)
            {
                if (i == 4 || i == 9) code[i] = '-';
                else code[i] = chars[_rng.Next(chars.Length)];
            }
            return new string(code);
        }
    }
}
