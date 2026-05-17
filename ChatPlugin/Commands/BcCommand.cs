using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;

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

                // LabAPI: 遍历所有玩家发送广播
                foreach (var target in Player.List)
                {
                    if (target != null)
                    {
                        target.ClearBroadcasts();
                        target.SendBroadcast(formatted, cfg.BcDuration);
                    }
                }

                if (cfg.LogChat)
                    Logger.Info($"[全体] {sender.LogName}: {message}");

                response = "全体消息已发送";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"BC命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }
    }
}
