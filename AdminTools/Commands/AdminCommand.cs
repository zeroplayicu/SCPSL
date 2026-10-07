using System;
using System.Text;
using CommandSystem;
using Exiled.API.Features;

namespace AdminTools.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class AdminCommand : ICommand
    {
        public string Command => "admin";
        public string[] Aliases => new[] { "adm", "管理员" };
        public string Description =>
            "管理员权限管理: admin add <lv3|lv4|lv5|lv6> <uid|steam> <标识符> | admin del <uid|steam> <标识符>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var plugin = AdminTools.Instance;
                if (plugin == null)
                {
                    response = "AdminTools 插件未加载";
                    return false;
                }

                // ---- 校验执行者权限 ----
                bool isConsole = sender is ServerConsoleSender
                    || sender.LogName == "SERVER CONSOLE"
                    || sender.LogName == "Server"
                    || sender.LogName == "localhost";

                // 服务器控制台(终端/主机)视为最高权限，允许使用（可通过配置 AllowConsole 关闭）
                if (isConsole && plugin.Config.AllowConsole)
                {
                    Log.Info($"[AdminTools] {sender.LogName} 从服务器控制台执行 admin 指令");
                    // 继续执行，不校验等级
                }
                else
                {
                    // 非控制台：必须是游戏内 lv5/lv6 玩家
                    var executor = Player.Get(sender);
                    if (executor == null)
                    {
                        Log.Warn($"[AdminTools] {sender.LogName} 无法解析为游戏内玩家，已拒绝");
                        response = "无法解析执行者，请以游戏内玩家身份或服务器控制台使用该指令";
                        return false;
                    }
                    string executorUserId = executor.UserId;
                    int executorLevel = plugin.Manager.GetAdminLevel(executorUserId);

                    if (executorLevel < plugin.Config.MinManageLevel)
                    {
                        Log.Warn($"[AdminTools] {sender.LogName}(等级{executorLevel}) 尝试使用 admin 指令被拒绝");
                        response = $"权限不足：需要 {AdminManager.GetLevelName(plugin.Config.MinManageLevel)} 及以上(等级{plugin.Config.MinManageLevel})才能使用该指令";
                        return false;
                    }
                }

                // ---- 参数解析 ----
                if (arguments.Count < 1)
                {
                    response = GetHelp();
                    return false;
                }

                string subCmd = arguments.At(0).ToLower();

                switch (subCmd)
                {
                    case "add":
                        return HandleAdd(arguments, plugin, sender, out response);
                    case "del":
                    case "delete":
                    case "remove":
                        return HandleDelete(arguments, plugin, sender, out response);
                    case "list":
                    case "查询":
                        return HandleList(plugin, out response);
                    case "help":
                        response = GetHelp();
                        return true;
                    default:
                        response = GetHelp();
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] admin 指令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }

        private bool HandleAdd(ArraySegment<string> arguments, AdminTools plugin, ICommandSender sender, out string response)
        {
            if (arguments.Count < 4)
            {
                response = "用法: admin add <lv3|lv4|lv5|lv6> <uid|steam> <标识符>";
                return false;
            }

            string lvlArg = arguments.At(1).ToLower();
            string idType = arguments.At(2).ToLower();
            string identifier = arguments.At(3).Trim();

            int level = lvlArg switch
            {
                "lv3" => 3,
                "lv4" => 4,
                "lv5" => 5,
                "lv6" => 6,
                _ => 0
            };

            if (level == 0)
            {
                response = "等级无效，仅支持 lv3/lv4/lv5/lv6";
                return false;
            }

            string err = plugin.Manager.ResolveUserId(idType, identifier, out string targetUserId, out string displayName);
            if (err != null)
            {
                response = err;
                return false;
            }

            plugin.Manager.SetAdminLevel(targetUserId, level);

            // 应用权限到在线玩家
            var target = Player.Get(targetUserId);
            if (target != null)
                plugin.Manager.ApplyPermissions(target, level);

            Log.Info($"[AdminTools] {sender.LogName} 将 {displayName}({targetUserId}) 设为 {AdminManager.GetLevelName(level)}");
            response = $"已将 {displayName} 设为 {AdminManager.GetLevelName(level)}(lv{level})";
            return true;
        }

        private bool HandleDelete(ArraySegment<string> arguments, AdminTools plugin, ICommandSender sender, out string response)
        {
            if (arguments.Count < 3)
            {
                response = "用法: admin del <uid|steam> <标识符>";
                return false;
            }

            string idType = arguments.At(1).ToLower();
            string identifier = arguments.At(2).Trim();

            string err = plugin.Manager.ResolveUserId(idType, identifier, out string targetUserId, out string displayName);
            if (err != null)
            {
                response = err;
                return false;
            }

            if (!plugin.Manager.IsAdmin(targetUserId))
            {
                response = $"{displayName} 当前不是管理员";
                return false;
            }

            int oldLevel = plugin.Manager.GetAdminLevel(targetUserId);
            plugin.Manager.SetAdminLevel(targetUserId, 0);

            var target = Player.Get(targetUserId);
            if (target != null)
                plugin.Manager.ApplyPermissions(target, 0);

            Log.Info($"[AdminTools] {sender.LogName} 移除 {displayName}({targetUserId}) 的 {AdminManager.GetLevelName(oldLevel)}");
            response = $"已移除 {displayName} 的管理员身份";
            return true;
        }

        private bool HandleList(AdminTools plugin, out string response)
        {
            var admins = plugin.Manager.GetAllAdmins();
            var sb = new StringBuilder();
            if (admins.Count == 0)
            {
                response = "目前没有管理员";
                return true;
            }
            sb.AppendLine("══ 管理员列表 ══");
            foreach (var kvp in admins)
            {
                sb.AppendLine($"{AdminManager.GetLevelName(kvp.Value)}(lv{kvp.Value}) — {kvp.Key}");
            }
            response = sb.ToString();
            return true;
        }

        private static string GetHelp()
        {
            return "══ AdminTools ══\n" +
                   "admin add <lv3|lv4|lv5|lv6> <uid|steam> <标识符>  — 添加/调整管理员\n" +
                   "admin del <uid|steam> <标识符> — 移除管理员\n" +
                   "admin list — 查看管理员列表\n" +
                   "说明: lv3=见习admin lv4=普通admin lv5=高级admin lv6=服主；uid=体验插件状态栏UID，steam=Steam64ID；仅 lv5/lv6 可用";
        }
    }
}
