using System;
using System.Collections.Generic;
using System.IO;
using CommandSystem;
using Exiled.API.Features;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ChatPlugin.Commands
{
    /// <summary>
    /// 查看生涯数据 - .info
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public class InfoCommand : ICommand
    {
        public string Command => "info";
        public string[] Aliases => new[] { "career", "stats" };
        public string Description => "查看你的生涯数据（游玩时长、击杀、KD等）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                // 解析玩家名
                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                // 从 ExperiencePlugin 的 YAML 数据文件中读取生涯数据
                string dataFile = Path.Combine(Paths.Exiled, "ExperienceData", "player_data.yml");
                if (!File.Exists(dataFile))
                {
                    response = $"{sender.LogName} 的生涯数据:\n<color=yellow>⏱ 游玩时长:</color> 暂无数据\n<color=yellow>💀 击杀:</color> 0\n<color=yellow>☠ 死亡:</color> 0\n<color=yellow>📊 KD:</color> 0.00\n<color=yellow>🏆 段位:</color> 无";
                    return true;
                }

                // 读取并反序列化 YAML
                string yaml = File.ReadAllText(dataFile);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var allData = deserializer.Deserialize<Dictionary<string, PlayerCareerData>>(yaml);
                if (allData == null)
                {
                    response = "无法读取数据文件";
                    return false;
                }

                // 按昵称匹配（ICommandSender 只有 LogName 可用）
                PlayerCareerData myData = null;
                foreach (var kvp in allData)
                {
                    if (kvp.Value?.PlayerName == playerName || kvp.Value?.PlayerName == sender.LogName)
                    {
                        myData = kvp.Value;
                        break;
                    }
                }

                if (myData == null)
                {
                    response = $"{sender.LogName} 的生涯数据:\n<color=yellow>⏱ 游玩时长:</color> 暂无数据\n<color=yellow>💀 击杀:</color> 0\n<color=yellow>☠ 死亡:</color> 0\n<color=yellow>📊 KD:</color> 0.00\n<color=yellow>🏆 段位:</color> 无";
                    return true;
                }

                // 计算数据
                string playTime = myData.GetPlayTimeString();
                int kills = myData.TotalKills;
                int deaths = myData.TotalDeaths;
                double kd = deaths > 0 ? Math.Round((double)kills / deaths, 2) : kills;

                // 计算生涯总时长（从创建到今天）
                string sinceCreated = "";
                if (myData.CreatedTime > DateTime.MinValue)
                {
                    var span = DateTime.Now - myData.CreatedTime;
                    if (span.TotalDays >= 1)
                        sinceCreated = $"（{span.Days}天前加入）";
                }

                string result = $"<size=20><color=#FFD700>══ {myData.PlayerName} 生涯数据 ══</color></size>\n" +
                    $"<color=yellow>⏱ 游玩时长:</color> <color=white>{playTime}</color>\n" +
                    $"<color=yellow>💀 总击杀:</color> <color=white>{kills}</color>\n" +
                    $"<color=yellow>☠ 总死亡:</color> <color=white>{deaths}</color>\n" +
                    $"<color=yellow>📊 KD:</color> <color=white>{kd:F2}</color>\n" +
                    $"<color=yellow>🏆 段位:</color> <color=gray>无</color>\n" +
                    $"<color=yellow>🎯 等级:</color> <color=white>{myData.Level}</color> {sinceCreated}";

                if (ChatPlugin.Instance.Config.LogChat)
                    Log.Info($"[Info] {playerName}: 查看了生涯数据");

                response = result;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Info命令错误: {ex.Message}");
                response = "获取数据失败";
                return false;
            }
        }
    }

    /// <summary>
    /// 生涯数据模型（映射自 ExperiencePlugin 的 YAML 文件）
    /// </summary>
    public class PlayerCareerData
    {
        public string UserId { get; set; }
        public string PlayerName { get; set; }
        public int Experience { get; set; }
        public int Level { get; set; }
        public int TotalPlayTimeMinutes { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public DateTime LastLoginTime { get; set; }
        public DateTime CreatedTime { get; set; }

        public string GetPlayTimeString()
        {
            int hours = TotalPlayTimeMinutes / 60;
            int minutes = TotalPlayTimeMinutes % 60;
            return hours > 0 ? $"{hours}小时{minutes}分钟" : $"{minutes}分钟";
        }
    }
}
