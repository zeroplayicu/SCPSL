using System;
using CommandSystem;
using Exiled.API.Features;
using RemoteAdmin;

namespace FactionPlugin.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class Scp999Command : ICommand
    {
        public string Command => "SCP999";
        public string[] Aliases => new[] { "999", "spawn999" };
        public string Description => "召唤SCP-999 - 轻收容出生 + 机枪治疗 + 黑卡（管理面板专用）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "你没有权限执行此命令";
                return false;
            }

            Player target = null;

            if (arguments.Count > 0)
            {
                if (int.TryParse(arguments.At(0), out int pid))
                    target = Player.Get(pid);
                if (target == null)
                    target = Player.Get(arguments.At(0));
            }

            if (target == null)
            {
                // 随机选一个观察者
                foreach (var p in Player.List)
                {
                    if (p != null && p.IsConnected && !p.IsAlive && !Scp999Manager.IsScp999(p))
                    {
                        target = p;
                        break;
                    }
                }
            }

            if (target == null || !target.IsConnected)
            {
                response = "没有可用的玩家来召唤SCP-999";
                return false;
            }

            if (Scp999Manager.IsScp999(target))
            {
                response = $"{target.Nickname} 已经是SCP-999";
                return false;
            }

            Scp999Manager.SpawnScp999(target);

            foreach (var p in Player.List)
            {
                if (p == null) continue;
                p.Broadcast(5, $"<color=#FF69B4>[SCP-999] {target.Nickname} 已变身为友好的SCP-999！靠近它将获得治疗</color>");
            }

            response = $"{target.Nickname} 已变身为SCP-999";
            return true;
        }
    }
}
