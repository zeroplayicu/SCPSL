using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class CCommand : ICommand
    {
        public string Command => "c";
        public string[] Aliases => new[] { "team", "t" };
        public string Description => "发送团队聊天消息（同阵营可见）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1 || string.IsNullOrWhiteSpace(arguments.At(0)))
                {
                    response = "用法: .c <消息内容>";
                    return false;
                }

                string message = string.Join(" ", arguments);
                var cfg = ExperiencePlugin.Instance.Config;

                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                var player = Player.List?.FirstOrDefault(p =>
                    p != null && p.Nickname == playerName);
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                var team = player.Role.Team;

                string nameColor = GetTeamColor(team, cfg);
                string badge = cfg.ShowRoleBadge ? GetRoleBadge(player) : "";

                string chatColor = "#FFFFFF";
                var data = ExperiencePlugin.Instance.DataManager?.GetPlayerData(player.UserId);
                if (data != null && !string.IsNullOrEmpty(data.ChatColor))
                    chatColor = data.ChatColor;

                // 注意：消息最终由 HSM 层渲染，不支持内联 <size>（会显示字面量），字号由该层 FontSize 控制
                string formatted = $"{cfg.CPrefix} {badge}<color={nameColor}>{playerName}</color>: <color={chatColor}>{message}</color>";

                ChatMessageBuffer.AddC(formatted, team);

                ExperiencePlugin.Instance.EventHandler.RefreshAllDisplays();
                int count = Player.List.Count(p => p != null && p.Role.Team == team);

                if (cfg.LogChat)
                    Log.Info($"[团队][{team}] {sender.LogName}: {message} ({count}人)");

                response = $"团队消息已发送 (同阵营{count}人)";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"C命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }

        private static string GetTeamColor(Team team, ExperienceConfig cfg)
        {
            return team switch
            {
                Team.SCPs => cfg.ScpColor,
                Team.FoundationForces => cfg.MtfColor,
                Team.ChaosInsurgency => cfg.ChaosColor,
                Team.Scientists => cfg.ScientistColor,
                Team.ClassD => cfg.ClassDColor,
                _ => cfg.OtherColor
            };
        }

        private static string GetRoleBadge(Player player)
        {
            if (player == null) return "";
            string roleName = player.Role.Type.ToString();

            if (player.Role.Team == Team.SCPs)
                return $"<color=#FF5555><b>[{roleName}]</b></color> ";

            if (player.Role.Team == Team.FoundationForces)
            {
                if (roleName.Contains("Captain") || roleName.Contains("Commander"))
                    return "<color=#5599FF><b>[NTF指挥官]</b></color> ";
                return "<color=#5599FF><b>[NTF]</b></color> ";
            }

            if (player.Role.Team == Team.ChaosInsurgency)
                return "<color=#55DD55><b>[CHAOS]</b></color> ";

            if (player.Role.Team == Team.Scientists)
                return "<color=#FFDD44><b>[科学家]</b></color> ";

            if (player.Role.Team == Team.ClassD)
                return "<color=#FF9922><b>[D级]</b></color> ";

            if (roleName.Contains("Guard"))
                return "<color=#88AACC><b>[警卫]</b></color> ";

            if (roleName.Contains("Tutorial"))
                return "<color=#CCCCCC><b>[教程]</b></color> ";

            return "";
        }
    }
}
