using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

namespace ExperiencePlugin.Commands
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
                var cfg = ExperiencePlugin.Instance.Config;

                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                // BC消息：精简格式，带倒计时
                ChatMessageBuffer.AddBc(playerName, message, cfg.BcDuration);

                if (cfg.LogChat)
                    Log.Info($"[全体] {sender.LogName}: {message}");

                ExperiencePlugin.Instance.EventHandler.RefreshAllDisplays();

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
