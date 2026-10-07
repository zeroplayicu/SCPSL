using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace ExpWebBate
{
    public class AuthManager
    {
        private readonly DataStore _store;
        private readonly ExpWebConfig _config;
        private readonly Dictionary<string, DateTime> _sessions = new Dictionary<string, DateTime>();
        private const int SessionHours = 8;
        private bool _passwordChanged;

        public AuthManager(DataStore store, ExpWebConfig config)
        {
            _store = store;
            _config = config;
        }

        /// <summary>尝试登录，返回 session token</summary>
        public string TryLogin(string username, string password)
        {
            // 统一使用绑定码登录（管理员和玩家都是6位绑定码）
            if (password.Length != 6 || !int.TryParse(password, out _))
                return null;

            string steamId = FindSteamIdByBindCode(password);
            if (steamId == null) return null;

            // 判断是管理员还是普通玩家
            var adminData = _store.LoadAdmin();
            string adminUserId = _config.AdminUser; // admin@steam 或类似

            bool isAdmin = (steamId == adminUserId) || IsAdminByReflection(steamId);

            string token = Guid.NewGuid().ToString("N");
            if (isAdmin)
            {
                _sessions[token] = DateTime.Now.AddHours(SessionHours);
                _passwordChanged = true; // 管理员用绑定码，不需要改密码
                return token;
            }
            else
            {
                _sessions[token] = DateTime.Now.AddHours(1);
                return "player:" + token + ":" + steamId;
            }
        }

        /// <summary>通过反射检查该 SteamId 是否为 ExperiencePlugin 管理员</summary>
        private bool IsAdminByReflection(string userId)
        {
            try
            {
                // 检查 CDK 管理或 Config 中的管理员列表
                // 简单方案：如果 steamId 包含 admin 关键词则视为管理员
                if (userId.ToLower().Contains("admin"))
                    return true;

                // 通过反射检查 ExperiencePlugin 配置中的 AdminUser
                var expPlugin = GetExpPluginInstance();
                if (expPlugin != null)
                {
                    var configProp = expPlugin.GetType().GetProperty("Config");
                    if (configProp != null)
                    {
                        var config = configProp.GetValue(expPlugin);
                        if (config != null)
                        {
                            var adminUserProp = config.GetType().GetProperty("AdminUser");
                            if (adminUserProp != null)
                            {
                                string adminUser = adminUserProp.GetValue(config)?.ToString();
                                if (adminUser == userId)
                                    return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>通过绑定码查找 SteamId（通过反射读取 ExperiencePlugin 的 BindManager）</summary>
        private string FindSteamIdByBindCode(string code)
        {
            try
            {
                var expPlugin = GetExpPluginInstance();
                if (expPlugin == null) return null;

                var bindMgrProp = expPlugin.GetType().GetProperty("BindManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (bindMgrProp == null) return null;

                var bindMgr = bindMgrProp.GetValue(expPlugin);
                if (bindMgr == null) return null;

                var method = bindMgr.GetType().GetMethod("GetSteamIdByCode",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method == null) return null;

                return method.Invoke(bindMgr, new object[] { code }) as string;
            }
            catch { return null; }
        }

        /// <summary>获取 ExperiencePlugin 实例</summary>
        private static object GetExpPluginInstance()
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in asm)
                {
                    if (a.GetName().Name == "ExperiencePlugin")
                    {
                        var t = a.GetType("ExperiencePlugin.ExperiencePlugin");
                        if (t != null)
                        {
                            var prop = t.GetProperty("Instance",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (prop != null)
                                return prop.GetValue(null);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>验证 session 是否有效</summary>
        public bool ValidateSession(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (_sessions.ContainsKey(token))
            {
                if (DateTime.Now < _sessions[token])
                    return true;
                _sessions.Remove(token);
            }
            return false;
        }

        /// <summary>获取 session 关联的玩家ID（仅 player token）</summary>
        public string GetPlayerIdFromToken(string token)
        {
            if (string.IsNullOrEmpty(token) || !token.StartsWith("player:"))
                return null;
            var parts = token.Split(':');
            return parts.Length >= 3 ? parts[2] : null;
        }

        /// <summary>是否是管理员 session</summary>
        public bool IsAdminSession(string token)
        {
            return !string.IsNullOrEmpty(token) && _sessions.ContainsKey(token) && !token.StartsWith("player:");
        }

        /// <summary>修改管理员密码</summary>
        public bool ChangeAdminPassword(string token, string oldPwd, string newPwd)
        {
            if (!IsAdminSession(token)) return false;
            if (!ValidateSession(token)) return false;

            var adminData = _store.LoadAdmin();
            string storedPwd = adminData != null && adminData.ContainsKey("password")
                ? adminData["password"].ToString()
                : _config.AdminPassword;

            if (oldPwd != storedPwd) return false;

            if (adminData == null) adminData = new Dictionary<string, object>();
            adminData["password"] = newPwd;
            _store.SaveAdmin(adminData);
            _passwordChanged = true;
            return true;
        }

        /// <summary>是否需要修改密码</summary>
        public bool NeedPasswordChange()
        {
            return !_passwordChanged;
        }

        /// <summary>退出登录</summary>
        public void Logout(string token)
        {
            if (!string.IsNullOrEmpty(token))
                _sessions.Remove(token);
        }
    }
}
