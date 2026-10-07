using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class CdkAddCommand : ICommand
    {
        public string Command => "cdkadd";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "生成CDK激活码: cdkadd [数量] [天数] [VIP/SVIP]";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 3)
                {
                    response = "用法: cdkadd [数量] [天数] [VIP/SVIP]\n例: cdkadd 10 30 VIP";
                    return false;
                }

                if (!int.TryParse(arguments.At(0), out int count) || count < 1 || count > 100)
                {
                    response = "数量必须为1-100的整数";
                    return false;
                }

                if (!int.TryParse(arguments.At(1), out int days) || days < -1 || days > 3650)
                {
                    response = "天数必须为0(永久)-3650的整数";
                    return false;
                }

                string type = arguments.At(2).ToUpper();
                if (type != "VIP" && type != "SVIP")
                {
                    response = "类型必须为 VIP 或 SVIP";
                    return false;
                }

                var codes = ExperiencePlugin.Instance.CdkManager.Generate(count, type, days);
                string durationStr = days <= 0 ? "永久" : $"{days}天";

                response = $"已生成{count}个{type}CDK({durationStr}):\n{string.Join("\n", codes)}";
                Log.Info($"[CDK] {sender.LogName} 生成了{count}个{type}CDK({durationStr})");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"CDKADD错误: {ex.Message}");
                response = "生成CDK失败";
                return false;
            }
        }
    }
}
