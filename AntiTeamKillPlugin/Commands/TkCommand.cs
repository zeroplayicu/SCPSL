using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;

namespace AntiTeamKillPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class TkCommand : ICommand
    {
        public string Command => "tk";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "被队友击杀后可发起开庭 (.tk)；管理员确认开庭 (.tk yes <案件号>)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;

                // .tk yes <案件号> —— 管理员确认开庭
                if (arguments.Count >= 1 && arguments.At(0).ToLower() == "yes")
                {
                    if (!player.RemoteAdminAccess)
                    {
                        response = "你没有权限确认开庭";
                        return false;
                    }
                    if (arguments.Count < 2)
                    {
                        response = "用法: .tk yes <案件号>";
                        return false;
                    }

                    string caseId = arguments.At(1);
                    if (handler.GetTkCase(caseId) == null)
                    {
                        response = $"未找到案件号 \"{caseId}\"，案件可能已处理或不存在";
                        return false;
                    }

                    if (handler.ConfirmTkCase(player, caseId))
                    {
                        response = $"已确认开庭(案件号 {caseId.ToUpperInvariant()})，相关人员已变为教程角色";
                        return true;
                    }
                    response = "开庭确认失败，请检查案件号";
                    return false;
                }

                // .tk —— 受害者发起开庭（允许死亡/旁观状态，被队友击杀后即可发起）
                string killerId = handler.GetKillerUserId(player.UserId);
                if (killerId == null)
                {
                    response = "未检测到你被任何玩家击杀，无法发起开庭";
                    return false;
                }

                // 验证击杀者是否同队（队友击杀才可开庭）
                var killer = Player.List.FirstOrDefault(p => p != null && p.UserId == killerId);
                if (killer == null)
                {
                    response = "击杀者已不在服务器，无法发起开庭";
                    return false;
                }

                string newCaseId = handler.StartTkCase(player);
                if (newCaseId == null)
                {
                    response = "无法发起开庭，请确保你确实被队友击杀";
                    return false;
                }

                response = $"<color=#FFD700>⚖ 你已发起开庭</color>\n<color=white>案件号: <color=#00FF00>{newCaseId}</color></color>\n<color=#AAAAAA>已通知在线管理员，请等待管理员输入 <color=#FFD700>.tk yes {newCaseId}</color> 确认开庭</color>";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($".tk命令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }
    }
}
