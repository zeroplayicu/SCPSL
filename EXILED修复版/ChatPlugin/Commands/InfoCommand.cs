using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ChatPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class InfoCommand : ICommand
    {
        public string Command => "info";
        public string[] Aliases => new[] { "career", "stats" };
        public string Description => "查看你的生涯数据（游玩时长、击杀、KD等）";

        // BUG-17修复: 增加内存缓存，避免每次 .info 都全量读取+反序列化整个 player_data.yml。
        // 缓存有效期 CacheSeconds 秒；同一时间窗口内多人查询只读一次文件。
        // 注: 数据可能在缓存期内被 ExperiencePlugin 修改，但对"查看生涯数据"这类
        //     非实时场景，几秒的延迟是可接受的（原实现每次读盘在千人数据下代价更高）。
        private static Dictionary<string, PlayerCareerData> _cache;
        private static DateTime _cacheTime = DateTime.MinValue;
        private const int CacheSeconds = 5;
        private static readonly object _cacheLock = new object();

        private static string GetDataFile() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EXILED", "ExperienceData", "player_data.yml");

        /// <summary>BUG-17修复: 带缓存的加载</summary>
        private static Dictionary<string, PlayerCareerData> LoadDataCached(string dataFile)
        {
            lock (_cacheLock)
            {
                if (_cache != null && (DateTime.Now - _cacheTime).TotalSeconds < CacheSeconds)
                    return _cache;

                string yaml = File.ReadAllText(dataFile);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                _cache = deserializer.Deserialize<Dictionary<string, PlayerCareerData>>(yaml)
                         ?? new Dictionary<string, PlayerCareerData>();
                _cacheTime = DateTime.Now;
                return _cache;
            }
        }

        /// <summary>BUG-10同类修复: 回合开始时使缓存失效</summary>
        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cache = null;
                _cacheTime = DateTime.MinValue;
            }
        }

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                string dataFile = GetDataFile();

                // UI-04修复: 命令回显是纯文本（控制台/RA 不解析富文本），
                // 富文本标签会被原样显示；emoji 在游戏字体下也可能显示为方块，统一改用 ASCII 标签
                if (!File.Exists(dataFile))
                {
                    response = $"{player.Nickname} 的生涯数据:\n[时长] 暂无数据\n[击杀] 0\n[死亡] 0\n[KD] 0.00\n[段位] 无";
                    return true;
                }

                var allData = LoadDataCached(dataFile);
                if (allData == null)
                {
                    response = "无法读取数据文件";
                    return false;
                }

                PlayerCareerData myData = null;
                if (!string.IsNullOrEmpty(player.UserId) && allData.TryGetValue(player.UserId, out var exactMatch))
                    myData = exactMatch;
                else
                {
                    // 回退: 按昵称匹配（BUG-32同类，改用玩家对象的昵称而非 sender.LogName 解析）
                    string playerName = player.Nickname;
                    foreach (var kvp in allData)
                    {
                        if (kvp.Value?.PlayerName == playerName)
                        { myData = kvp.Value; break; }
                    }
                }

                if (myData == null)
                {
                    response = $"{player.Nickname} 的生涯数据:\n[时长] 暂无数据\n[击杀] 0\n[死亡] 0\n[KD] 0.00\n[段位] 无";
                    return true;
                }

                string playTime = myData.GetPlayTimeString();
                int kills = myData.TotalKills;
                int deaths = myData.TotalDeaths;
                double kd = deaths > 0 ? Math.Round((double)kills / deaths, 2) : kills;

                string sinceCreated = "";
                if (myData.CreatedTime > DateTime.MinValue)
                {
                    var span = DateTime.Now - myData.CreatedTime;
                    if (span.TotalDays >= 1)
                        sinceCreated = $"（{span.Days}天前加入）";
                }

                string result = $"══ {myData.PlayerName} 生涯数据 ══\n" +
                    $"[时长] {playTime}\n" +
                    $"[击杀] {kills}\n" +
                    $"[死亡] {deaths}\n" +
                    $"[KD] {kd:F2}\n" +
                    $"[段位] 暂无\n" +
                    $"[等级] {myData.Level} {sinceCreated}";

                if (ChatPlugin.Instance.Config.LogChat)
                    Log.Info($"[Info] {player.Nickname}: 查看了生涯数据");

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
