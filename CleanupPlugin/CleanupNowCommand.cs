using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using RemoteAdmin;

namespace CleanupPlugin
{
    /// <summary>
    /// RA 控制台命令：ql —— 管理员立即执行一次完整清理（掉落物 + 尸体，血包/权限卡保留）。
    /// 跳过"开局免清扫期"和"有物品才清理"的判断，立即执行。
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class CleanupNowCommand : ICommand
    {
        public string Command => "ql";
        public string[] Aliases => new[] { "cleanupnow", "saodi" };
        public string Description => "立即执行一次完整清理（掉落物+尸体，血包/权限卡保留）";
        public bool SanitizeResponse => false;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            // 服务器控制台（非玩家）直接放行；玩家需要设施管理权限
            bool fromConsole = !(sender is PlayerCommandSender);
            if (!fromConsole && !sender.CheckPermission(PlayerPermissions.FacilityManagement))
            {
                response = "你没有权限执行该命令（需要设施管理权限）";
                return false;
            }

            var plugin = CleanupPlugin.Instance;
            if (plugin == null)
            {
                response = "CleanupPlugin 未加载";
                return false;
            }

            if (!Round.IsStarted)
            {
                response = "对局尚未开始，无法清理";
                return false;
            }

            plugin.ForceCleanupNow();
            response = "已执行完整清理（掉落物 + 尸体；血包/权限卡保留）";
            return true;
        }
    }
}
