using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace ChatPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BcCommand : ICommand
    {
        public string Command => "bc";
        public string[] Aliases => new[] { "broadcast", "all" };
        public string Description => "发送全体聊天消息";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (arguments.Count < 1 || string.IsNullOrWhiteSpace(arguments.At(0)))
                {
                    response = "用法: .bc <消息内容>";
                    return false;
                }

                string message = string.Join(" ", arguments);
                var cfg = ChatPlugin.Instance.Config;

                // 查找发送者玩家对象
                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                var player = Player.List?.FirstOrDefault(p =>
                    p != null && p.Nickname == playerName);

                // 获取阵营颜色和角色徽章
                string nameColor = GetTeamColor(player, cfg);
                string badge = cfg.ShowRoleBadge ? GetRoleBadge(player) : "";

                // 梦时镜风格: 【全服】 [徽章] 玩家名: 消息
                string formatted = $"<size={cfg.FontSize}>{cfg.BcPrefix} {badge}<color={nameColor}>{playerName}</color>: <color={cfg.MessageColor}>{message}</color></size>";

                // 堆叠到缓冲区（team=null 表示全体可见）
                ChatMessageBuffer.Add(formatted, null);

                // 刷新所有玩家的显示
                foreach (var target in Player.List)
                {
                    if (target == null) continue;
                    string display = ChatMessageBuffer.BuildFor(target.Role.Team);
                    target.ClearBroadcasts();
                    target.Broadcast(cfg.BcDuration, display, Broadcast.BroadcastFlags.Normal);
                }

                if (cfg.LogChat)
                    Log.Info($"[全体] {sender.LogName}: {message}");

                response = "全体消息已发送";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"BC命令错误: {ex.Message}");
                response = "发送失败";
                return false;
            }
        }

        /// <summary>根据玩家阵营获取颜色</summary>
        private static string GetTeamColor(Player player, ChatConfig cfg)
        {
            if (player == null) return cfg.OtherColor;

            return player.Role.Team switch
            {
                Team.SCPs => cfg.ScpColor,
                Team.FoundationForces => cfg.MtfColor,
                Team.ChaosInsurgency => cfg.ChaosColor,
                Team.Scientists => cfg.ScientistColor,
                Team.ClassD => cfg.ClassDColor,
                _ => cfg.OtherColor
            };
        }

        /// <summary>根据玩家角色获取徽章文本（富文本格式）</summary>
        private static string GetRoleBadge(Player player)
        {
            if (player == null) return "";

            string roleName = player.Role.Type.ToString();

            // SCP 角色直接显示编号
            if (player.Role.Team == Team.SCPs)
                return $"<color=#FF5555><b>[{roleName}]</b></color> ";

            // NTF / MTF
            if (player.Role.Team == Team.FoundationForces)
            {
                if (roleName.Contains("Captain") || roleName.Contains("Commander"))
                    return "<color=#5599FF><b>[NTF指挥官]</b></color> ";
                return "<color=#5599FF><b>[NTF]</b></color> ";
            }

            // 混沌分裂者
            if (player.Role.Team == Team.ChaosInsurgency)
                return "<color=#55DD55><b>[CHAOS]</b></color> ";

            // 科学家
            if (player.Role.Team == Team.Scientists)
                return "<color=#FFDD44><b>[科学家]</b></color> ";

            // D级
            if (player.Role.Team == Team.ClassD)
                return "<color=#FF9922><b>[D级]</b></color> ";

            // 警卫
            if (roleName.Contains("Guard"))
                return "<color=#88AACC><b>[警卫]</b></color> ";

            // 教程
            if (roleName.Contains("Tutorial"))
                return "<color=#CCCCCC><b>[教程]</b></color> ";

            return "";
        }
    }
}