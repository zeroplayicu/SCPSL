using System;
using CommandSystem;
using Exiled.API.Features;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class CdkCommand : ICommand
    {
        public string Command => "cdk";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "兑换CDK激活VIP";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1)
                {
                    response = "用法: .cdk <激活码>";
                    return false;
                }

                string code = arguments.At(0).ToUpper();
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var entry = ExperiencePlugin.Instance.CdkManager.Redeem(code, player);
                if (entry == null)
                {
                    response = "CDK无效或已被使用!";
                    return false;
                }

                string typeName = entry.Type == "SVIP" ? "SVIP" : "VIP";
                string duration = entry.Days <= 0 ? "永久" : $"{entry.Days}天";
                response = $"成功兑换{typeName}({duration})! 经验倍率: {GetMultiplier(entry.Type)}x";
                Log.Info($"[CDK] {player.Nickname} 兑换了{typeName} CDK: {code}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"CDK错误: {ex.Message}");
                response = "兑换失败";
                return false;
            }
        }

        private static string GetMultiplier(string type)
        {
            return type == "SVIP"
                ? ExperiencePlugin.Instance.Config.SvipExpMultiplier.ToString("F1")
                : ExperiencePlugin.Instance.Config.VipExpMultiplier.ToString("F1");
        }
    }
}
