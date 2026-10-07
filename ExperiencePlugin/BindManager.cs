using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;

namespace ExperiencePlugin
{
    /// <summary>
    /// 绑定管理器 - 管理 SteamId ↔ 绑定码 的映射关系
    /// 绑定码为玩家在游戏内输入的随机6位数，用于在网页端登录
    /// </summary>
    public class BindService
    {
        private readonly string _dataFile;
        private readonly Dictionary<string, string> _steamToCode;   // SteamId → 绑定码
        private readonly Dictionary<string, string> _codeToSteam;   // 绑定码 → SteamId
        private readonly Random _random = new Random();
        private readonly object _lock = new object();

        public BindService(string dataDirectory)
        {
            _dataFile = Path.Combine(dataDirectory, "bind_data.yml");
            _steamToCode = new Dictionary<string, string>();
            _codeToSteam = new Dictionary<string, string>();
            Load();
        }

        /// <summary>为玩家生成一个唯一的6位绑定码</summary>
        public string GenerateBindCode(string userId)
        {
            lock (_lock)
            {
                // 如果已有绑定码，直接返回旧的
                if (_steamToCode.TryGetValue(userId, out string existing))
                    return existing;

                // 生成不重复的6位码
                string code;
                do
                {
                    code = _random.Next(100000, 999999).ToString();
                } while (_codeToSteam.ContainsKey(code));

                _steamToCode[userId] = code;
                _codeToSteam[code] = userId;
                Save();
                return code;
            }
        }

        /// <summary>通过绑定码获取SteamId</summary>
        public string GetSteamIdByCode(string code)
        {
            lock (_lock)
            {
                _codeToSteam.TryGetValue(code, out string steamId);
                return steamId;
            }
        }

        /// <summary>通过SteamId获取绑定码</summary>
        public string GetCodeBySteamId(string userId)
        {
            lock (_lock)
            {
                _steamToCode.TryGetValue(userId, out string code);
                return code;
            }
        }

        /// <summary>是否已注册（生成了绑定码但可能未绑定）</summary>
        public bool HasRegistered(string userId)
        {
            lock (_lock) { return _steamToCode.ContainsKey(userId); }
        }

        /// <summary>是否已绑定（双向映射存在）</summary>
        public bool IsBound(string userId)
        {
            lock (_lock)
            {
                if (_steamToCode.TryGetValue(userId, out string code))
                    return _codeToSteam.ContainsKey(code) && _codeToSteam[code] == userId;
                return false;
            }
        }

        /// <summary>获取待绑定的码（注册后未绑定的状态）</summary>
        public string GetPendingCode(string userId)
        {
            lock (_lock)
            {
                _steamToCode.TryGetValue(userId, out string code);
                return code;
            }
        }

        /// <summary>删除绑定</summary>
        public void Unbind(string userId)
        {
            lock (_lock)
            {
                if (_steamToCode.TryGetValue(userId, out string code))
                {
                    _steamToCode.Remove(userId);
                    _codeToSteam.Remove(code);
                    Save();
                }
            }
        }

        /// <summary>强制设置绑定关系（由 .bind <码> 使用）</summary>
        public void ForceBind(string userId, string code)
        {
            lock (_lock)
            {
                // 清除旧的绑定关系
                if (_steamToCode.TryGetValue(userId, out string oldCode))
                {
                    _codeToSteam.Remove(oldCode);
                }
                if (_codeToSteam.TryGetValue(code, out string oldUser))
                {
                    _steamToCode.Remove(oldUser);
                }

                _steamToCode[userId] = code;
                _codeToSteam[code] = userId;
                Save();
            }
        }

        /// <summary>获取所有绑定数据（用于Web端登录验证）</summary>
        public Dictionary<string, string> GetAllBinds()
        {
            lock (_lock) { return new Dictionary<string, string>(_steamToCode); }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_dataFile)) return;
                string[] lines = File.ReadAllLines(_dataFile);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        string steamId = parts[0].Trim();
                        string code = parts[1].Trim();
                        _steamToCode[steamId] = code;
                        _codeToSteam[code] = steamId;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[BindManager] 加载绑定数据失败: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_dataFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var lines = _steamToCode.Select(kvp => $"{kvp.Key}|{kvp.Value}");
                File.WriteAllLines(_dataFile, lines);
            }
            catch (Exception ex)
            {
                Log.Error($"[BindManager] 保存绑定数据失败: {ex.Message}");
            }
        }
    }
}
