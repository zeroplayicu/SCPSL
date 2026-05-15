using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

namespace ChatPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BcCommand : ICommand
    {
        public string Command => "bc";
        public string[] Aliases => new[] { "broadcast", "all" };
        public string Description => "发送全体聊天消息";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1 || string.IsNullOrWhiteSpace(arguments.At(0)))
                {
                    response = "用法: .bc <消息内容>";
                    return false;
                }

                string message = string.Join(" ", arguments);
                var cfg = ChatPlugin.Instance.Config;

                string formatted = $"<size={cfg.FontSize}>{cfg.BcPrefix} <color=white>{sender.LogName}</color>: <color=white>{message}</color></size>";

                // 清除旧广播避免重叠
                Map.ClearBroadcasts();
                foreach (var target in Player.List)
                {
                    if (target != null)
                        target.Broadcast(cfg.BcDuration, formatted);
                }

                if (cfg.LogChat)
                    Log.Info($"[全体] {sender.LogName}: {message}");

                response = "全体消息已发送";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"BC命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }
    }
}
