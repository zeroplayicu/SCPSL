using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AdminTools
{
    /// <summary>
    /// 管理员等级管理（lv3=见习admin, lv4=普通admin, lv5=高级admin, lv6=服主）
    /// 数据持久化到 %AppData%\EXILED\AdminTools\admins.yml
    /// </summary>
    public class AdminManager
    {
        private readonly AdminTools _plugin;
        private readonly Dictionary<string, int> _adminLevels; // userId -> 等级
        private readonly string _dataDirectory;
        private readonly string _dataFile;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;

        public AdminManager(AdminTools plugin)
        {
            _plugin = plugin;
            _adminLevels = new Dictionary<string, int>();

            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EXILED", "AdminTools");
            _dataFile = Path.Combine(_dataDirectory, "admins.yml");

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithIndentedSequences()
                .Build();

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            if (!Directory.Exists(_dataDirectory))
                Directory.CreateDirectory(_dataDirectory);

            Load();
            Log.Info($"[AdminTools] 已加载 {_adminLevels.Count} 条管理员记录");
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_dataFile))
                {
                    string yaml = File.ReadAllText(_dataFile);
                    if (!string.IsNullOrWhiteSpace(yaml))
                    {
                        var data = _deserializer.Deserialize<Dictionary<string, int>>(yaml);
                        if (data != null)
                        {
                            _adminLevels.Clear();
                            foreach (var kvp in data)
                                _adminLevels[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] 加载管理员数据失败: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                string yaml = _serializer.Serialize(_adminLevels);
                File.WriteAllText(_dataFile, yaml);
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] 保存管理员数据失败: {ex.Message}");
            }
        }

        /// <summary>设置管理员等级(3-6)，level为0表示移除</summary>
        public void SetAdminLevel(string userId, int level)
        {
            if (level >= 3 && level <= 6)
                _adminLevels[userId] = level;
            else
                _adminLevels.Remove(userId);
            Save();
        }

        public int GetAdminLevel(string userId)
        {
            return _adminLevels.TryGetValue(userId, out int lvl) ? lvl : 0;
        }

        public bool IsAdmin(string userId)
        {
            int lvl = GetAdminLevel(userId);
            return lvl >= 3;
        }

        public bool CanManage(string userId, int minLevel)
        {
            return GetAdminLevel(userId) >= minLevel;
        }

        public Dictionary<string, int> GetAllAdmins() => _adminLevels;

        /// <summary>根据标识符解析目标 UserId。identifierType: uid 或 steam</summary>
        public string ResolveUserId(string identifierType, string identifier, out string resolvedId, out string displayName)
        {
            resolvedId = null;
            displayName = identifier;
            try
            {
                if (identifierType == "steam")
                {
                    // Steam64ID -> UserId ("76561199767972428" -> "76561199767972428@steam")
                    string steamId = identifier.Trim();
                    if (!long.TryParse(steamId, out _))
                        return "Steam64ID无效（应为一串数字）";

                    // 先查在线玩家
                    var online = Player.List.FirstOrDefault(p => p != null && p.UserId.Split('@')[0] == steamId);
                    if (online != null)
                    {
                        resolvedId = online.UserId;
                        displayName = online.Nickname;
                        return null;
                    }

                    // 离线：查 ExperiencePlugin 数据文件
                    var data = FindInExperienceData(steamId, isUid: false);
                    if (data != null)
                    {
                        resolvedId = data.UserId;
                        displayName = data.PlayerName;
                        return null;
                    }

                    return $"未找到 Steam64ID 为 {steamId} 的玩家(在线或数据中均无)";
                }
                else if (identifierType == "uid")
                {
                    if (!int.TryParse(identifier.Trim(), out int uid) || uid <= 0)
                        return "UID无效（应为一个正整数，见玩家状态栏 UID:xxx）";

                    // 先查在线玩家
                    var online = Player.List.FirstOrDefault(p => p != null && GetOnlineUid(p.UserId) == uid);
                    if (online != null)
                    {
                        resolvedId = online.UserId;
                        displayName = online.Nickname;
                        return null;
                    }

                    // 离线：查 ExperiencePlugin 数据文件
                    var data = FindInExperienceData(uid.ToString(), isUid: true);
                    if (data != null)
                    {
                        resolvedId = data.UserId;
                        displayName = data.PlayerName;
                        return null;
                    }

                    return $"未找到 UID 为 {uid} 的玩家(在线或数据中均无)";
                }

                return "标识符类型无效，仅支持 uid 或 steam";
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] 解析目标出错: {ex.Message}");
                return "解析目标出错";
            }
        }

        /// <summary>读取 ExperiencePlugin 数据文件中的玩家 UID（在线玩家通过体验插件状态栏显示）</summary>
        private static int GetOnlineUid(string userId)
        {
            try
            {
                // 从 ExperiencePlugin 状态栏读取不到，这里通过加载其数据文件查找
                var data = FindInExperienceDataByUserId(userId);
                return data?.Uid ?? 0;
            }
            catch { return 0; }
        }

        private static ExperienceDataEntry FindInExperienceData(string identifier, bool isUid)
        {
            var entries = LoadExperienceData();
            foreach (var e in entries)
            {
                if (isUid)
                {
                    if (e.Uid.ToString() == identifier) return e;
                }
                else
                {
                    if (!string.IsNullOrEmpty(e.UserId) && e.UserId.Split('@')[0] == identifier) return e;
                }
            }
            return null;
        }

        private static ExperienceDataEntry FindInExperienceDataByUserId(string userId)
        {
            var entries = LoadExperienceData();
            foreach (var e in entries)
            {
                if (e.UserId == userId) return e;
            }
            return null;
        }

        private static List<ExperienceDataEntry> LoadExperienceData()
        {
            var result = new List<ExperienceDataEntry>();
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EXILED", "ExperienceData", "player_data.yml");

                if (!File.Exists(path)) return result;

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var data = deserializer.Deserialize<Dictionary<string, ExperienceDataEntry>>(File.ReadAllText(path));
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        kvp.Value.UserId = kvp.Key;
                        result.Add(kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] 读取体验插件数据失败: {ex.Message}");
            }
            return result;
        }

        /// <summary>应用到在线玩家：授予远程管理员面板访问 + 按等级授予指令权限</summary>
        public void ApplyPermissions(Player player, int level)
        {
            try
            {
                if (player == null || !player.IsConnected) return;

                var refHub = player.ReferenceHub;
                if (refHub == null || refHub.serverRoles == null) return;

                if (level >= 3)
                {
                    // 授予远程管理面板访问权限 + 按等级设置玩家权限位(PlayerPermissions)
                    // 通过 UserGroup 设置，保证既能打开管理员面板，又能按等级区分指令
                    // BadgeText/BadgeColor 置空：玩家列表徽章由 ExperiencePlugin 的 RankName 统一显示
                    //（若 Group.BadgeText 非空会覆盖 RankName，导致 VIP/SVIP/管理头衔无法显示）
                    var group = new UserGroup
                    {
                        Name = "lv" + level,
                        Permissions = GetLevelPermissions(level),
                        KickPower = 255,
                        RequiredKickPower = 0,
                        Cover = false,
                        HiddenByDefault = false,
                        BadgeText = "",
                        BadgeColor = ""
                    };
                    refHub.serverRoles.Group = group;
                    // 确保 RemoteAdmin 属性为 true（面板可打开）
                    refHub.serverRoles.RemoteAdmin = true;
                }
                else
                {
                    refHub.serverRoles.RemoteAdmin = false;
                    try { refHub.serverRoles.Group = null; } catch { }
                }

                Log.Info($"[AdminTools] 已更新 {player.Nickname} 的管理员权限(等级 {level}, 权限位 0x{GetLevelPermissions(level):X})");
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] 应用权限失败: {ex.Message}");
            }
        }

        /// <summary>根据等级计算 PlayerPermissions 权限位掩码</summary>
        private ulong GetLevelPermissions(int level)
        {
            // 使用游戏原生 PlayerPermissions 位掩码（UserGroup.Permissions 是 UInt64）
            // 参考: ForceclassSelf=8, ForceclassToSpectator=16, ForceclassWithoutRestrictions=32,
            //       GivingItems=64, PlayersManagement=16384, Noclip=2097152, Effects=67108864 ...
            ulong lv3 = 8UL | 16UL | 32UL | 16384UL;          // 变教程/改角色/传送(玩家管理)
            ulong lv4 = lv3 | 64UL;                            // + 刷物(GivingItems)
            ulong lv5 = ulong.MaxValue;                        // 所有权限(任意指令)

            return level switch
            {
                3 => lv3,
                4 => lv4,
                _ => lv5 // lv5/lv6 所有权限
            };
        }

        private static string GetLevelColor(int level)
        {
            return level switch
            {
                6 => "magenta",
                5 => "red",
                4 => "yellow",
                3 => "lime",
                _ => "white"
            };
        }

        /// <summary>等级名称</summary>
        public static string GetLevelName(int level)
        {
            return level switch
            {
                6 => "服主",
                5 => "高级admin",
                4 => "普通admin",
                3 => "见习admin",
                _ => "无"
            };
        }
    }

    /// <summary>ExperiencePlugin player_data.yml 的解析用结构</summary>
    internal class ExperienceDataEntry
    {
        public string UserId { get; set; }
        public string PlayerName { get; set; }
        public int Uid { get; set; }
    }
}
