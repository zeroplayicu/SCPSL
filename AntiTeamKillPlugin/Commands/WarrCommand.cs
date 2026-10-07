using System;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace AntiTeamKillPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class WarrCommand : ICommand
    {
        public string Command => "warr";
        public string[] Aliases => new[] { "warning", "warn" };
        public string Description => "查看/管理警告 (管理员可用 .warr list / .warr add <编码>)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;

                if (arguments.Count == 0)
                {
                    var warnings = handler.GetWarnings(player.UserId);
                    if (warnings.Count == 0)
                    {
                        response = $"<color=green>✅ 你没有任何警告记录</color>";
                        return true;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"<size=18><color=#FFD700>══ 你的警告记录 ({warnings.Count}条) ══</color></size>");
                    for (int i = 0; i < warnings.Count; i++)
                        sb.AppendLine($"<color=white>{i + 1}. {warnings[i]}</color>");
                    response = sb.ToString();
                    return true;
                }

                if (!player.RemoteAdminAccess)
                {
                    response = "你没有权限使用此命令";
                    return false;
                }

                string subCmd = arguments.At(0).ToLower();

                if (subCmd == "list")
                {
                    var tutorials = handler.GetTutorialPlayers();
                    if (tutorials.Count == 0)
                    {
                        response = "<color=yellow>当前没有教程角色玩家</color>";
                        return true;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"<size=18><color=#FFD700>══ 教程角色列表 ══</color></size>");
                    for (int i = 0; i < tutorials.Count; i++)
                    {
                        var t = tutorials[i];
                        // 取 @ 前的数字ID末4位作为编码（原直接取 UserId 末4位 = "team"，所有玩家编码相同）
                        string idPart = t.UserId.Split('@')[0];
                        string code = idPart.Length >= 4 ? idPart.Substring(idPart.Length - 4) : idPart;
                        int warns = handler.GetTotalWarnings(t.UserId);
                        sb.AppendLine($"<color=white>{i + 1}. {t.Nickname}</color> <color=#AAAAAA>编码:{code} 警告:{warns}</color>");
                    }
                    response = sb.ToString();
                    return true;
                }

                if (subCmd == "add" && arguments.Count >= 2)
                {
                    string code = arguments.At(1).ToLower();
                    var tutorials = handler.GetTutorialPlayers();
                    var target = tutorials.FirstOrDefault(t =>
                    {
                        string idPart = t.UserId.Split('@')[0];
                        return idPart.Length >= 4 && idPart.Substring(idPart.Length - 4).ToLower() == code;
                    });

                    if (target == null)
                    {
                        response = $"未找到编码为 \"{code}\" 的教程角色";
                        return false;
                    }

                    string warnMsg = arguments.Count >= 3
                        ? string.Join(" ", arguments.Skip(2))
                        : $"管理员 {player.Nickname} 发出警告";

                    handler.AddWarning(target.UserId, warnMsg);
                    Log.Info($"[警告] 管理员 {player.Nickname} 对 {target.Nickname} 发出警告: {warnMsg}");

                    response = $"已对 {target.Nickname} 添加警告: {warnMsg}";
                    return true;
                }

                response = "用法: .warr 查看自己警告 | .warr list 列出教程 | .warr add <编码> [内容]";
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($".warr命令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }
    }
}
