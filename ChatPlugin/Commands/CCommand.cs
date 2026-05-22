using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace ChatPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class CCommand : ICommand
    {
        public string Command => "c";
        public string[] Aliases => new[] { "team", "t" };
        public string Description => "发送团队聊天消息（同阵营可见）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1 || string.IsNullOrWhiteSpace(arguments.At(0)))
                {
                    response = "用法: .c <消息内容>";
                    return false;
                }

                string message = string.Join(" ", arguments);
                var cfg = ChatPlugin.Instance.Config;

                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                var player = Player.List?.FirstOrDefault(p =>
                    p != null && (p.Nickname == playerName || p.DisplayName == sender.LogName));
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                var team = player.Role.Team;
                string formatted = $"<size={cfg.FontSize}>{cfg.CPrefix} <color=white>{playerName}</color>: <color=white>{message}</color></size>";

                int count = 0;
                foreach (var target in Player.List)
                {
                    if (target != null && target.Role.Team == team)
                    {
                        target.ClearBroadcasts();
                        target.Broadcast(cfg.CDuration, formatted, Broadcast.BroadcastFlags.Normal);
                        count++;
                    }
                }

                if (cfg.LogChat)
                    Log.Info($"[团队][{team}] {sender.LogName}: {message} ({count}人)");

                response = $"团队消息已发送 (同阵营{count}人)";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"C命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }
    }
}
