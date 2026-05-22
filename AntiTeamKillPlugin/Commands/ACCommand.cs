using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

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
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;
                handler.AddAdminMessage(player.Nickname, player.UserId, message);
                Log.Info($"[玩家→管理] {player.Nickname}: {message}");

                response = $"消息已发送给管理员: {message}";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($".AC命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }
    }
}
