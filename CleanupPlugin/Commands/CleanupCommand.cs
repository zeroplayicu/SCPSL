using System;
using CommandSystem;
using Exiled.API.Features;

namespace CleanupPlugin.Commands
{
    /// <summary>
    /// 管理员手动清理指令：clanr
    /// 在管理员面板终端(RemoteAdmin)或服务器控制台输入 clanr 即可清理所有掉落物和尸体。
    /// 特殊武器和 SCP 物品不清理。
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class CleanupCommand : ICommand
    {
        public string Command => "clanr";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "清理所有掉落物和尸体（特殊武器和SCP物品不清理）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var plugin = CleanupPlugin.Instance;
                if (plugin == null)
                {
                    response = "CleanupPlugin 插件未加载";
                    return false;
                }

                // ---- 校验执行者权限：仅允许服务器控制台或拥有 RemoteAdmin 权限的玩家 ----
                bool isConsole = sender is ServerConsoleSender
                    || sender.LogName == "SERVER CONSOLE"
                    || sender.LogName == "Server"
                    || sender.LogName == "localhost";

                if (!isConsole)
                {
                    var executor = Player.Get(sender);
                    if (executor == null)
                    {
                        response = "无法解析执行者，请以游戏内管理员身份或服务器控制台使用该指令";
                        return false;
                    }

                    // 必须有远程管理(管理员)权限
                    if (executor.ReferenceHub == null || !executor.ReferenceHub.serverRoles.RemoteAdmin)
                    {
                        Log.Warn($"[clanr] {executor.Nickname}({executor.UserId}) 无管理员权限，已拒绝");
                        response = "权限不足：需要管理员权限才能使用 clanr 指令";
                        return false;
                    }
                }
                else
                {
                    Log.Info($"[clanr] {sender.LogName} 从服务器控制台执行手动清理");
                }

                // ---- 执行手动清理 ----
                plugin.ManualCleanup(out int pickupRemoved, out int ragdollRemoved, out int skippedScp);
                response = $"已清理 {pickupRemoved} 个掉落物 + {ragdollRemoved} 具尸体（跳过SCP/特殊武器 {skippedScp} 个）";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[clanr] 指令执行出错: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }
    }
}
