using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace ChatPlugin.Commands
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
                var cfg = ChatPlugin.Instance.Config;

                // BUG-33修复: 改用 Player.Get(sender) 获取权威玩家对象，
                // 避免昵称解析歧义与 `Player.List?.FirstOrDefault` 的空引用问题
                var player = Player.Get(sender);
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }
                string playerName = player.Nickname;

                var team = player.Role.Team;

                // 获取阵营颜色和角色徽章
                string nameColor = GetTeamColor(team, cfg);
                string badge = cfg.ShowRoleBadge ? GetRoleBadge(player) : "";

                // 梦时镜风格: 【团队】 [徽章] 玩家名: 消息
                // UI-03修复: 玩家输入先剥离富文本标签，防止 <size=2000> 等注入刷屏/破坏堆叠排版
                string safeMessage = RichTextSanitizer.Sanitize(message);
                string formatted = $"<size={cfg.FontSize}>{cfg.CPrefix} {badge}<color={nameColor}>{playerName}</color>: <color={cfg.MessageColor}>{safeMessage}</color></size>";

                // 堆叠到缓冲区（指定team，仅同阵营可见）
                ChatMessageBuffer.Add(formatted, team);

                // 刷新同阵营玩家的显示
                // UI-02修复: 改用 HSM 下发；不再 ClearBroadcasts()（会清掉其他插件的广播）
                int count = 0;
                foreach (var target in Player.List)
                {
                    if (target == null || !target.IsConnected || target.Role.Team != team) continue;
                    string display = ChatMessageBuffer.BuildFor(target.Role.Team);
                    ChatHintDisplay.Show(target, display, cfg.FontSize, cfg.CDuration);
                    count++;
                }

                if (cfg.LogChat)
                    Log.Info($"[团队][{team}] {player.Nickname}: {message} ({count}人)");

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

        /// <summary>根据阵营获取颜色</summary>
        private static string GetTeamColor(Team team, ChatConfig cfg)
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