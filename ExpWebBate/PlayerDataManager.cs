using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;

namespace ExpWebBate
{
    /// <summary>通过反射读取 ExperiencePlugin 的玩家数据</summary>
    public class PlayerDataManager
    {
        private Type _expPluginType;
        private Type _playerDataType;
        private object _expInstance;
        private object _dataManager;
        private MethodInfo _getAllDataMethod;
        private MethodInfo _getPlayerDataMethod;

        public PlayerDataManager()
        {
            try
            {
                // 查找 ExperiencePlugin 程序集
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ExperiencePlugin");
                if (asm == null)
                {
                    Log.Warn("[ExpWebBate] 未找到 ExperiencePlugin 程序集");
                    return;
                }

                _expPluginType = asm.GetType("ExperiencePlugin.ExperiencePlugin");
                _playerDataType = asm.GetType("ExperiencePlugin.PlayerData");

                if (_expPluginType != null)
                {
                    var instanceProp = _expPluginType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                        _expInstance = instanceProp.GetValue(null);

                    if (_expInstance != null)
                    {
                        var dmField = _expPluginType.GetField("DataManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var dmProp = _expPluginType.GetProperty("DataManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        _dataManager = dmField?.GetValue(_expInstance) ?? dmProp?.GetValue(_expInstance);

                        if (_dataManager != null)
                        {
                            _getAllDataMethod = _dataManager.GetType().GetMethod("GetAllPlayerData",
                                BindingFlags.Public | BindingFlags.Instance);
                            _getPlayerDataMethod = _dataManager.GetType().GetMethod("GetPlayerData",
                                BindingFlags.Public | BindingFlags.Instance);
                        }
                    }
                }

                if (_dataManager == null)
                    Log.Warn("[ExpWebBate] 无法获取 ExperiencePlugin 数据管理器");
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate] 初始化 PlayerDataManager 失败: " + ex.Message);
            }
        }

        /// <summary>获取所有玩家数据</summary>
        public List<PlayerDataEntry> GetAllPlayers()
        {
            var result = new List<PlayerDataEntry>();
            try
            {
                if (_getAllDataMethod == null) return result;

                var rawData = _getAllDataMethod.Invoke(_dataManager, null);
                if (rawData == null) return result;

                var dict = rawData as System.Collections.IDictionary;
                if (dict == null)
                {
                    // 可能是 IEnumerable
                    var enumerable = rawData as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        foreach (var item in enumerable)
                        {
                            var entry = ExtractEntry(item);
                            if (entry != null) result.Add(entry);
                        }
                    }
                    return result;
                }

                foreach (var key in dict.Keys)
                {
                    var val = dict[key];
                    var entry = ExtractEntry(val);
                    if (entry != null) result.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate] GetAllPlayers 错误: " + ex.Message);
            }
            return result;
        }

        /// <summary>获取单个玩家数据</summary>
        public PlayerDataEntry GetPlayer(string userId)
        {
            try
            {
                if (_getPlayerDataMethod == null) return null;
                var data = _getPlayerDataMethod.Invoke(_dataManager, new object[] { userId });
                return ExtractEntry(data);
            }
            catch { return null; }
        }

        private PlayerDataEntry ExtractEntry(object data)
        {
            if (data == null || _playerDataType == null) return null;
            try
            {
                var entry = new PlayerDataEntry();

                System.Reflection.MemberInfo propLevel = _playerDataType.GetProperty("Level") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("Level");
                System.Reflection.MemberInfo propExp = _playerDataType.GetProperty("Experience") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("Experience");
                System.Reflection.MemberInfo propPoints = _playerDataType.GetProperty("Points") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("Points");
                System.Reflection.MemberInfo propUserId = _playerDataType.GetProperty("UserId") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("UserId");
                System.Reflection.MemberInfo propName = _playerDataType.GetProperty("Nickname") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("Nickname");
                System.Reflection.MemberInfo propVip = _playerDataType.GetProperty("VipLevel") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("VipLevel");
                System.Reflection.MemberInfo propKills = _playerDataType.GetProperty("TotalKills") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("TotalKills");
                System.Reflection.MemberInfo propDeaths = _playerDataType.GetProperty("TotalDeaths") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("TotalDeaths");
                System.Reflection.MemberInfo propExpToNext = _playerDataType.GetProperty("ExperienceToNextLevel") ?? (System.Reflection.MemberInfo)_playerDataType.GetField("ExperienceToNextLevel");

                entry.Level = GetInt(propLevel, data);
                entry.Experience = GetInt(propExp, data);
                entry.Points = GetFloat(propPoints, data);
                entry.UserId = GetString(propUserId, data);
                entry.Nickname = GetString(propName, data);
                entry.VipLevel = GetInt(propVip, data);
                entry.TotalKills = GetInt(propKills, data);
                entry.TotalDeaths = GetInt(propDeaths, data);
                entry.ExpToNext = GetInt(propExpToNext, data);

                // 获取当前在线玩家信息
                var player = Player.Get(entry.UserId);
                if (player != null)
                {
                    entry.IsOnline = true;
                    entry.CurrentRole = player.Role.Type.ToString();
                }

                return entry;
            }
            catch { return null; }
        }

        private int GetInt(System.Reflection.MemberInfo prop, object obj)
        {
            if (prop == null) return 0;
            if (prop is PropertyInfo pi) return Convert.ToInt32(pi.GetValue(obj));
            if (prop is FieldInfo fi) return Convert.ToInt32(fi.GetValue(obj));
            return 0;
        }

        private float GetFloat(System.Reflection.MemberInfo prop, object obj)
        {
            if (prop == null) return 0;
            if (prop is PropertyInfo pi) return Convert.ToSingle(pi.GetValue(obj));
            if (prop is FieldInfo fi) return Convert.ToSingle(fi.GetValue(obj));
            return 0;
        }

        private string GetString(System.Reflection.MemberInfo prop, object obj)
        {
            if (prop == null) return "";
            if (prop is PropertyInfo pi) return pi.GetValue(obj)?.ToString() ?? "";
            if (prop is FieldInfo fi) return fi.GetValue(obj)?.ToString() ?? "";
            return "";
        }
    }

    public class PlayerDataEntry
    {
        public string UserId { get; set; }
        public string Nickname { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int ExpToNext { get; set; }
        public float Points { get; set; }
        public int VipLevel { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public bool IsOnline { get; set; }
        public string CurrentRole { get; set; }
    }
}
