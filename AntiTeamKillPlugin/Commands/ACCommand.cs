using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;

namespace AntiTeamKillPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ACCommand : ICommand
    {
        public string Command => "AC";
        public string[] Aliases => new[] { "ac", "adminchat", "report" };
        public string Description => "向管理员发送消息 (.AC <内容>)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1 || string.IsNullOrWhiteSpace(arguments.At(0)))
                {
                    response = "用法: .AC <消息内容>";
                    return false;
                }

                string message = string.Join(" ", arguments);
                string playerName = sender.LogName;
                int idx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (idx > 0) playerName = playerName.Substring(0, idx);

                var player = Player.List.FirstOrDefault(p => p != null && p.Nickname == playerName);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;

                // 记录消息
                handler.AddAdminMessage(playerName, player.UserId, message);
                Logger.Info($"[玩家→管理] {playerName}: {message}");

                // 推送给所有在线管理员
                handler.ShowAdminMessagesToAdmins();

                response = $"消息已发送给管理员: {message}";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($".AC命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }
    }
}
