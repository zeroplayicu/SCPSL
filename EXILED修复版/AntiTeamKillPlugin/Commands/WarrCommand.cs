using System;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;

namespace AntiTeamKillPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class WarrCommand : ICommand
    {
        public string Command => "warr";
        public string[] Aliases => new[] { "warning", "warn" };
        public string Description => "查看/管理警告 (管理员可用 .warr list / .warr add <编码>)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var handler = AntiTeamKillPlugin.Instance.EventHandler;

                if (arguments.Count == 0)
                {
                    var warnings = handler.GetWarnings(player.UserId);
                    if (warnings.Count == 0)
                    {
                        // UI-04修复: 命令回显是纯文本（控制台/RA 不解析富文本），富文本标签会被原样显示
                        response = "✅ 你没有任何警告记录";
                        return true;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"══ 你的警告记录 ({warnings.Count}条) ══");
                    for (int i = 0; i < warnings.Count; i++)
                        sb.AppendLine($"{i + 1}. {warnings[i]}");
                    response = sb.ToString();
                    return true;
                }

                if (!player.RemoteAdminAccess)
                {
                    response = "你没有权限使用此命令";
                    return false;
                }

                // 一致性修复: 与 BUG-30 保持一致，改用 ToLowerInvariant 避免区域文化差异
                string subCmd = arguments.At(0).ToLowerInvariant();

                if (subCmd == "list")
                {
                    var tutorials = handler.GetTutorialPlayers();
                    if (tutorials.Count == 0)
                    {
                        response = "当前没有教程角色玩家";
                        return true;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine("══ 教程角色列表 ══");
                    for (int i = 0; i < tutorials.Count; i++)
                    {
                        var t = tutorials[i];
                        // UI-05修复: 编码取 UserId 数字主体尾部，不再取到 "@steam"
                        string code = GetPlayerCode(t);
                        int warns = handler.GetTotalWarnings(t.UserId);
                        sb.AppendLine($"{i + 1}. {t.Nickname} 编码:{code} 警告:{warns}");
                    }
                    response = sb.ToString();
                    return true;
                }

                if (subCmd == "add" && arguments.Count >= 2)
                {
                    string code = arguments.At(1).ToLowerInvariant();
                    var tutorials = handler.GetTutorialPlayers();
                    var target = tutorials.FirstOrDefault(t =>
                        GetPlayerCode(t).ToLowerInvariant() == code);

                    if (target == null)
                    {
                        response = $"未找到编码为 \"{code}\" 的教程角色";
                        return false;
                    }

                    string warnMsg = arguments.Count >= 3
                        ? string.Join(" ", arguments.Skip(2))
                        : $"管理员 {player.Nickname} 发出警告";

                    // UI-03修复: 警告内容回显给玩家时会进入富文本，先剥离标签防止注入破坏排版
                    string safeWarnMsg = RichTextSanitizer.Sanitize(warnMsg);
                    handler.AddWarning(target.UserId, safeWarnMsg);
                    Log.Info($"[警告] 管理员 {player.Nickname} 对 {target.Nickname} 发出警告: {warnMsg}");

                    response = $"已对 {target.Nickname} 添加警告: {safeWarnMsg}";
                    return true;
                }

                response = "用法: .warr 查看自己警告 | .warr list 列出教程 | .warr add <编码> [内容]";
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($".warr命令错误: {ex.Message}");
                response = "执行失败";
                return false;
            }
        }

        /// <summary>
        /// UI-05修复（BUG-16 修正）: 生成玩家唯一编码 = 昵称首字符 + UserId 数字主体后6位。
        /// 原先直接取 UserId 末尾 6 位，对 "76561198012345678@steam" 会得到 "@steam"，
        /// 导致所有玩家编码尾缀相同、几乎失去区分度（.warr add 极易误伤他人）。
        /// </summary>
        private static string GetPlayerCode(Player p)
        {
            if (p == null) return "";
            if (string.IsNullOrEmpty(p.UserId)) return p.Nickname ?? "";

            // 先截掉 @platform 后缀，只保留数字/ID 主体
            string id = p.UserId;
            int at = id.IndexOf('@');
            if (at >= 0) id = id.Substring(0, at);

            string tail = id.Length >= 6 ? id.Substring(id.Length - 6) : id;
            string head = string.IsNullOrEmpty(p.Nickname) ? "" : p.Nickname.Substring(0, 1);
            return head + tail;
        }
    }
}
