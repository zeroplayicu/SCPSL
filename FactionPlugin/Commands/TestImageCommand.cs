using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using RemoteAdmin;

namespace FactionPlugin
{
    /// <summary>
    /// 字符画 HUD 测试命令（2026-10-06）：
    ///   timg &lt;图名&gt; [fps]  —— 播放该图片/动画
    ///   timg stop            —— 停止并清除
    ///   timg font            —— 显示字符可用性测试（确认方块字符能否渲染）
    ///   timg list            —— 列出可用图名
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class TestImageCommand : ICommand
    {
        public string Command => "timg";
        public string[] Aliases => new[] { "testimage", "tuxiang" };
        public string Description => "字符画 HUD 测试：timg <图名> [fps] | timg stop | timg font | timg list";
        public bool SanitizeResponse => false;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            bool fromConsole = !(sender is PlayerCommandSender);
            if (!fromConsole && !sender.CheckPermission(PlayerPermissions.FacilityManagement))
            {
                response = "没有权限（需要设施管理）";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "用法: timg <图名> [fps] | timg stop | timg font | timg list";
                return false;
            }

            string sub = arguments.At(0).ToLowerInvariant();

            if (sub == "font")
            {
                // 字符可用性测试：确认哪些符号在游戏字体里能正常显示
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected) continue;
                    p.ShowHint(
                        "<color=#FFFFFF>[字符测试]</color>\n" +
                        "<size=22>█▉▊▋▌▍▎▏</size>\n" +
                        "<size=22>■□▪▫▬▭</size>\n" +
                        "<size=22>●○◘◙◖◗</size>\n" +
                        "<size=22>▀▄▐▌░▒▓</size>\n" +
                        "<size=22>★☆✦✧✱✳</size>\n" +
                        "<size=22>═━│┃┌┐└┘├┤┬┴┼</size>\n" +
                        "<size=22>= - | + o O # @</size>",
                        40f);
                }
                response = "已发送字符测试（看哪个能正常显示，把结果发我）";
                return true;
            }

            if (sub == "list")
            {
                try
                {
                    string root = ImageHudRenderer.ImagesRoot;
                    if (!System.IO.Directory.Exists(root)) { response = $"图库目录不存在: {root}"; return false; }
                    var dirs = System.IO.Directory.GetDirectories(root);
                    response = dirs.Length == 0 ? "暂无图片" : "可用图片: " + string.Join(", ", System.Array.ConvertAll(dirs, System.IO.Path.GetFileName));
                }
                catch (Exception ex) { response = "读取失败: " + ex.Message; }
                return true;
            }

            if (sub == "stop")
            {
                foreach (var p in Player.List)
                {
                    if (p != null && p.IsConnected) ImageHudRenderer.Stop(p);
                }
                response = "已停止并清除字符画";
                return true;
            }

            // 播放：timg <图名> [fps]
            float fps = 5f;
            if (arguments.Count > 1 && float.TryParse(arguments.At(1), out float f) && f > 0.3f && f <= 30f)
                fps = f;

            int frames = ImageHudRenderer.FrameCount(sub);
            if (frames <= 0)
            {
                response = $"找不到图片 '{sub}'（或没有帧数据）";
                return false;
            }

            int n = 0;
            foreach (var p in Player.List)
            {
                if (p == null || !p.IsConnected || !p.IsAlive) continue;
                ImageHudRenderer.Play(p, sub, fps, repeat: 1, blockN: 4, blockCols: 24, blockRows: 12, fontSize: 13, centerY: 420);
                n++;
            }
            response = $"正在播放 '{sub}'：{frames} 帧 @ {fps}fps，发给 {n} 名玩家";
            return true;
        }
    }
}
