using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using PlayerRoles;

namespace AntiTeamKillPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class MaCommand : ICommand
    {
        public string Command => "ma";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "被恶意组杀的观察者可变为教程角色";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                string playerName = sender.LogName;
                int idx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (idx > 0) playerName = playerName.Substring(0, idx);

                var player = Player.List.FirstOrDefault(p => p != null && p.Nickname == playerName);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                // 必须是观察者
                if (player.IsAlive) { response = "你必须在观察模式才能使用此命令"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;
                string killerId = handler.GetKillerUserId(player.UserId);

                if (killerId == null) { response = "无法找到击杀你的玩家"; return false; }

                var killer = Player.List.FirstOrDefault(p => p != null && p.UserId == killerId);
                if (killer == null) { response = "击杀者已不在服务器"; return false; }

                // 击杀者必须是教程角色（已被处罚）
                if (killer.Role != RoleTypeId.Tutorial)
                {
                    response = "击杀者未被处罚，无法使用此命令";
                    return false;
                }

                // 将自己变为教程角色
                player.SetRole(RoleTypeId.Tutorial, RoleChangeReason.Respawn, RoleSpawnFlags.All);
                Logger.Info($"[.ma] {player.Nickname} 被组杀后自愿变为教程角色 (击杀者:{killer.Nickname})");

                response = "你已变为教程角色，可查看教程区域了解游戏规则";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($".ma命令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }
    }
}
