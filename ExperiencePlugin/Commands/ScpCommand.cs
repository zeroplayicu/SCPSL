using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ScpCommand : ICommand
    {
        public string Command => "scp";
        public string[] Aliases => new[] { "scpself", "scps" };
        public string Description => "SCP自选: .scp 049/096/173/939/106/079/999 或 .scp info 查询";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var plugin = ExperiencePlugin.Instance;
                var data = plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) { response = "无法读取玩家数据"; return false; }

                string subCmd = arguments.Count > 0 ? arguments.At(0).ToLower() : "";

                // ---- .scp info → 查询剩余次数 ----
                if (subCmd == "info" || subCmd == "查询" || subCmd == "cd")
                {
                    plugin.ScpSelect.CheckDailyReset(data);
                    int maxDaily = plugin.ScpSelect.GetDailyMaxSelections(data);
                    int remaining = maxDaily - data.ScpSelectUsedToday;

                    string levelInfo = $"等级 {data.Level} → 基础 {1 + (data.Level / 5) * 2} 次";
                    string vipInfo = "";
                    if (data.VipLevel >= 2) vipInfo = $"\nSVIP 加成 +{plugin.Config.ScpSelectSvipBonus} 次";
                    else if (data.VipLevel >= 1) vipInfo = $"\nVIP 加成 +{plugin.Config.ScpSelectVipBonus} 次";

                    response = $"<color=#FF4B4B>🔴 SCP自选</color>\n" +
                               $"剩余次数: {remaining}/{maxDaily}\n" +
                               $"{levelInfo}{vipInfo}\n" +
                               $"等待队列: {plugin.ScpSelect.WaitingCount} 人\n" +
                               $"使用 .scp <编号> 提交自选";
                    return true;
                }

                // ---- 解析 SCP 编号 ----
                if (string.IsNullOrEmpty(subCmd))
                {
                    // 显示帮助
                    response = $"<color=#FF4B4B>🔴 SCP自选</color> | 可用: 049 096 173 939 106 079{GetScp999HelpSuffix()}\n" +
                               ".scp 049 - 自选SCP-049\n" +
                               ".scp info - 查询剩余次数";
                    return true;
                }

                // ---- SCP-999 自选 ----
                string normalized = subCmd.Replace("scp", "").Replace("-", "").Trim();
                if (normalized == "999")
                {
                    if (!TryScp999Select(player, out response))
                    {
                        response = "服务器未启用SCP-999自选功能";
                        return false;
                    }
                    return true;
                }

                // 尝试解析角色（支持 049, 96, scp049, Scp049 等格式）
                string roleName = normalized;
                if (roleName.Length == 2) roleName = "0" + roleName; // 96 → 096, 79 → 079

                RoleTypeId targetRole;
                if (!TryParseScpRole(roleName, out targetRole))
                {
                    response = $"无效的SCP编号: {subCmd}。支持: 049, 096, 173, 939, 106, 079{GetScp999HelpSuffix()}";
                    return false;
                }

                // 提交自选
                var result = plugin.ScpSelect.SubmitSelection(player, targetRole);
                response = result.Message;
                return result.Success;
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP自选] 命令错误: {ex.Message}");
                response = "SCP自选失败";
                return false;
            }
        }

        private static bool TryParseScpRole(string number, out RoleTypeId role)
        {
            role = RoleTypeId.None;
            string roleName = "Scp" + number.PadLeft(3, '0');

            // 特殊处理 079 (computer)
            if (roleName == "Scp079") roleName = "Scp079";

            return Enum.TryParse(roleName, true, out role);
        }

        /// <summary>
        /// 运行时检测 FactionPlugin 是否加载并包含 Scp999Manager。
        /// 没有该插件时 .scp 帮助/错误信息中不显示 999 选项。
        /// </summary>
        private static bool IsScp999Available()
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "FactionPlugin");
                if (asm == null) return false;

                return asm.GetType("FactionPlugin.Scp999Manager") != null;
            }
            catch
            {
                return false;
            }
        }

        private static string GetScp999HelpSuffix()
        {
            return IsScp999Available() ? " 999" : "";
        }

        /// <summary>
        /// 通过反射调用 FactionPlugin.Scp999Manager.AddToWaiting 加入SCP-999自选队列。
        /// 返回 true 表示SCP-999插件可用且已完成调用；false 表示插件未加载。
        /// </summary>
        private static bool TryScp999Select(Player player, out string response)
        {
            response = string.Empty;

            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "FactionPlugin");
                if (asm == null) return false;

                var managerType = asm.GetType("FactionPlugin.Scp999Manager");
                if (managerType == null) return false;

                var addMethod = managerType.GetMethod("AddToWaiting",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(Player) }, null);
                if (addMethod == null) return false;

                var result = (bool)addMethod.Invoke(null, new object[] { player });
                if (!result)
                {
                    var isMethod = managerType.GetMethod("IsScp999",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new[] { typeof(Player) }, null);
                    bool isScp999 = false;
                    if (isMethod != null)
                        isScp999 = (bool)isMethod.Invoke(null, new object[] { player });

                    response = isScp999
                        ? "你已经是SCP-999了"
                        : "加入SCP-999自选队列失败，请稍后再试";
                }
                else
                {
                    response = "你已加入SCP-999自选队列，开局(人数>15)将优先变身为SCP-999";
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[SCP自选] SCP-999 反射调用失败: {ex.Message}");
                return false;
            }
        }
    }
}
