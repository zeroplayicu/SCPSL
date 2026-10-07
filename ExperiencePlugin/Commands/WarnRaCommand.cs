using System;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class WarnRaCommand : ICommand
    {
        public string Command => "warn";
        public string[] Aliases => new[] { "warnings", "处罚" };
        public string Description => "警告管理: warn add/reform <玩家> [原因] | warn list/remove <玩家> [序号] | warn unban <玩家>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1)
                {
                    response = "用法: warn add <玩家> [原因] — 添加警告\n" +
                               "warn reform <玩家> [原因] — 添加警告+劳改\n" +
                               "warn unban <玩家> — 解除劳改\n" +
                               "warn list <玩家> — 查看警告\n" +
                               "warn remove <玩家> <序号> — 删除警告";
                    return false;
                }

                var plugin = ExperiencePlugin.Instance;
                string subCmd = arguments.At(0).ToLower();

                // ---- 不需要player名的命令 ----
                if (subCmd == "list" && arguments.Count < 2)
                {
                    var userIds = plugin.WarningManager.GetAllUserIds();
                    if (userIds.Count == 0)
                    {
                        response = "没有玩家有警告记录";
                        return true;
                    }
                    var sb = new StringBuilder();
                    sb.AppendLine("══ 警告记录总览 ══");
                    foreach (var uid in userIds)
                    {
                        var p = Player.List.FirstOrDefault(x => x.UserId == uid);
                        var data = plugin.DataManager.GetPlayerData(uid);
                        string name = p?.Nickname ?? uid;
                        int count = plugin.WarningManager.GetTotalWarnings(uid);
                        string reform = (data != null && data.IsLaborReform) ? " [劳改中]" : "";
                        sb.AppendLine($"{name} ({count}条){reform}");
                    }
                    response = sb.ToString();
                    return true;
                }

                if (arguments.Count < 2)
                {
                    response = "请指定玩家名";
                    return false;
                }

                string targetQuery = arguments.At(1);
                var targets = WarningManager.FindPlayers(targetQuery);

                if (targets.Count == 0)
                {
                    response = $"未找到匹配 \"{targetQuery}\" 的玩家";
                    return false;
                }

                var target = targets[0];
                if (targets.Count > 1)
                    Log.Warn($"[警告] 找到多个匹配，使用第一个: {target.Nickname}");

                var targetData = plugin.DataManager.GetPlayerData(target.UserId);
                if (targetData == null)
                {
                    response = "无法获取目标玩家数据";
                    return false;
                }

                string reason = arguments.Count >= 3
                    ? string.Join(" ", arguments.Skip(2))
                    : $"管理员 {sender.LogName} 发出警告";

                switch (subCmd)
                {
                    case "add":
                        plugin.WarningManager.AddWarning(target.UserId, reason);
                        targetData.IsLaborReform = false;
                        plugin.EventHandler.RefreshSingleVipBadge(target, targetData);
                        plugin.DataManager.SaveAllData();
                        Log.Info($"[警告] 管理员添加警告 → {target.Nickname}: {reason}");
                        response = $"已对 {target.Nickname} 添加警告: {reason}";
                        return true;

                    case "reform":
                    case "劳改":
                        plugin.WarningManager.AddWarning(target.UserId, reason);
                        targetData.IsLaborReform = true;
                        // 如果玩家在线，强制其变为D级（如果已生成）
                        if (target.IsAlive)
                        {
                            try { target.Role.Set(PlayerRoles.RoleTypeId.ClassD, Exiled.API.Enums.SpawnReason.Respawn); }
                            catch { }
                        }
                        plugin.EventHandler.ApplyLaborReform(target, targetData);
                        plugin.DataManager.SaveAllData();
                        Log.Info($"[警告] 管理员添加警告+劳改 → {target.Nickname}: {reason}");
                        string reformMsg = $"<size=20><color=#FF4444>⚠ 你已被管理员设为劳改状态!</color></size>\n" +
                            $"<size=14>原因: {reason}</size>";
                        target.ShowHint(reformMsg, 10);
                        response = $"已对 {target.Nickname} 添加警告并设为劳改状态: {reason}";
                        return true;

                    case "unban":
                    case "解除":
                        if (!targetData.IsLaborReform)
                        {
                            response = $"{target.Nickname} 当前不是劳改状态";
                            return false;
                        }
                        targetData.IsLaborReform = false;
                        plugin.WarningManager.AddWarning(target.UserId, $"管理员 {sender.LogName} 解除了劳改状态");
                        plugin.EventHandler.RefreshSingleVipBadge(target, targetData);
                        plugin.DataManager.SaveAllData();
                        Log.Info($"[警告] 管理员解除劳改 → {target.Nickname}");
                        target.ShowHint($"<size=20><color=green>✅ 你的劳改状态已被解除</color></size>", 8);
                        response = $"已解除 {target.Nickname} 的劳改状态";
                        return true;

                    case "list":
                        var warns = plugin.WarningManager.GetWarnings(target.UserId);
                        if (warns.Count == 0)
                        {
                            response = $"{target.Nickname} 没有警告记录";
                            return true;
                        }
                        var sb = new StringBuilder();
                        string rfStatus = targetData.IsLaborReform ? " [劳改中]" : "";
                        sb.AppendLine($"<size=18>══ {target.Nickname}{rfStatus} 警告记录 ({warns.Count}条) ══</size>");
                        for (int i = 0; i < warns.Count; i++)
                            sb.AppendLine($"{i + 1}. {warns[i]}");
                        response = sb.ToString();
                        return true;

                    case "remove":
                        if (arguments.Count < 3)
                        {
                            response = "请指定要删除的警告序号，可用 warn list <玩家> 查看";
                            return false;
                        }
                        if (!int.TryParse(arguments.At(2), out int idx) || idx < 1)
                        {
                            response = "序号无效";
                            return false;
                        }
                        if (plugin.WarningManager.RemoveWarning(target.UserId, idx - 1))
                        {
                            response = $"已删除 {target.Nickname} 的第 {idx} 条警告";
                            return true;
                        }
                        response = "删除失败，序号可能无效";
                        return false;

                    default:
                        response = "未知子命令，可用: add / reform / unban / list / remove";
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"WarnRA命令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }
    }
}
