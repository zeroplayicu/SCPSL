using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
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
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                if (player.IsAlive) { response = "你必须在观察模式才能使用此命令"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;
                // BUG-07修复: GetKillerUserId 内部已做有效期校验，过期击杀记录返回 null
                string killerId = handler.GetKillerUserId(player.UserId);
                if (killerId == null) { response = "未检测到有效的被击杀记录（可能已过期），无法使用此命令"; return false; }

                var killer = Player.List.FirstOrDefault(p => p != null && p.UserId == killerId);
                if (killer == null) { response = "击杀者已不在服务器"; return false; }

                if (killer.Role.Type != RoleTypeId.Tutorial)
                {
                    response = "击杀者未被处罚，无法使用此命令";
                    return false;
                }

                player.Role.Set(RoleTypeId.Tutorial, Exiled.API.Enums.SpawnReason.Respawn, RoleSpawnFlags.All);
                Log.Info($"[.ma] {player.Nickname} 被组杀后自愿变为教程角色 (击杀者:{killer.Nickname})");

                response = "你已变为教程角色，可查看教程区域了解游戏规则";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($".ma命令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }
    }
}
