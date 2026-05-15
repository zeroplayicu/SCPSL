using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

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

                // LogName 可能格式为 "昵称 (SteamID@steam)"，提取前面的昵称
                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                // 按昵称或UserId匹配当前玩家
                var player = Player.List?.FirstOrDefault(p =>
                    p != null && (p.Nickname == playerName || p.Nickname == sender.LogName));
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                var side = player.Role.Side;
                string formatted = $"<size={cfg.FontSize}>{cfg.CPrefix} <color=white>{playerName}</color>: <color=white>{message}</color></size>";

                // 清除旧广播避免重叠
                Map.ClearBroadcasts();
                int count = 0;
                foreach (var target in Player.List)
                {
                    if (target != null && target.Role.Side == side)
                    {
                        target.Broadcast(cfg.CDuration, formatted);
                        count++;
                    }
                }

                if (cfg.LogChat)
                    Log.Info($"[团队][{side}] {sender.LogName}: {message} ({count}人)");

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
