using System;
using CommandSystem;
using Exiled.API.Features;

namespace YulePlugin.Commands
{
    /// <summary>
    /// 娱乐模式指令：
    ///   yule on autojc    开启 自动把加入玩家变为教程角色
    ///   yule off autojc   关闭 自动把加入玩家变为教程角色
    ///   yule status       查看当前娱乐模式状态
    /// 支持管理员控制台(RemoteAdmin) + 服务器终端(ServerConsole)
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class YuleCommand : ICommand
    {
        public string Command => "yule";
        public string[] Aliases => new[] { "娱乐", "yl" };
        public string Description => "娱乐模式指令: yule on/off autojc（自动让加入玩家变为教程角色）| yule status";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var plugin = YulePlugin.Instance;
                if (plugin == null)
                {
                    response = "YulePlugin 插件未加载";
                    return false;
                }

                // ---- 权限校验：服务器控制台或游戏内管理员 ----
                bool isConsole = sender is ServerConsoleSender
                    || sender.LogName == "SERVER CONSOLE"
                    || sender.LogName == "Server"
                    || sender.LogName == "localhost";

                if (!isConsole)
                {
                    // 非控制台：必须是游戏内管理员(AdminTools lv3+，或有管理权限)
                    var executor = Player.Get(sender);
                    if (executor == null)
                    {
                        response = "无法解析执行者，请以游戏内管理员身份或服务器控制台使用该指令";
                        return false;
                    }
                    if (!plugin.IsAdmin(executor))
                    {
                        response = "权限不足：需要管理员权限才能使用该指令";
                        return false;
                    }
                }

                // ---- 无参数：显示帮助 ----
                if (arguments.Count < 1)
                {
                    response = "用法:\n" +
                               "  yule on autojc   — 开启 自动把加入玩家变为教程角色\n" +
                               "  yule off autojc  — 关闭 自动把加入玩家变为教程角色\n" +
                               "  yule status      — 查看当前娱乐模式状态";
                    return false;
                }

                string action = arguments.At(0).ToLowerInvariant();

                switch (action)
                {
                    case "on":
                    case "open":
                    case "开启":
                        return HandleOn(arguments, plugin, out response);
                    case "off":
                    case "close":
                    case "关闭":
                        return HandleOff(arguments, plugin, out response);
                    case "status":
                    case "状态":
                        response = plugin.GetStatusText();
                        return true;
                    default:
                        response = "未知指令。用法: yule on/off autojc 或 yule status";
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Yule] yule 指令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }

        private static bool HandleOn(ArraySegment<string> arguments, YulePlugin plugin, out string response)
        {
            if (arguments.Count < 2)
            {
                response = "用法: yule on <功能>，当前支持: autojc";
                return false;
            }

            string feature = arguments.At(1).ToLowerInvariant();
            if (feature != "autojc" && feature != "自动教程" && feature != "ajc")
            {
                response = "未知功能: " + feature + "。当前支持: autojc（自动教程）";
                return false;
            }

            plugin.AutoTutorial = true;
            Log.Info($"[Yule] 已开启 autojc（自动把加入玩家变为教程角色）");
            response = "✅ 已开启 autojc：新加入的玩家将自动变为教程角色";
            return true;
        }

        private static bool HandleOff(ArraySegment<string> arguments, YulePlugin plugin, out string response)
        {
            if (arguments.Count < 2)
            {
                response = "用法: yule off <功能>，当前支持: autojc";
                return false;
            }

            string feature = arguments.At(1).ToLowerInvariant();
            if (feature != "autojc" && feature != "自动教程" && feature != "ajc")
            {
                response = "未知功能: " + feature + "。当前支持: autojc（自动教程）";
                return false;
            }

            plugin.AutoTutorial = false;
            Log.Info($"[Yule] 已关闭 autojc");
            response = "已关闭 autojc：新加入的玩家不再自动变为教程角色";
            return true;
        }
    }
}
