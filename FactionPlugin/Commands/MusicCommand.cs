using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using RemoteAdmin;

namespace FactionPlugin
{
    /// <summary>
    /// 点歌台命令（2026-10-06）：
    ///   music list      —— 列出可点曲目
    ///   music &lt;编号&gt;    —— 点歌（广播栏显示歌名 + CASSIE 播报）
    ///   music stop      —— 停止并还原广播栏
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class MusicCommand : ICommand
    {
        public string Command => "music";
        public string[] Aliases => new[] { "dianjiangtai", "歌曲" };
        public string Description => "点歌台：music list | music <编号> | music stop";
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
                response = "用法: music list | music <编号> | music stop";
                return false;
            }

            string sub = arguments.At(0).ToLowerInvariant();

            if (sub == "list")
            {
                response = "可点曲目: " + MusicStation.ListSongs();
                return true;
            }

            if (sub == "spawn")
            {
                Player exec = null;
                if (sender is PlayerCommandSender pcs) exec = Player.Get(pcs.ReferenceHub);
                if (exec == null) { response = "请在游戏内以管理员身份站在目标位置执行该命令"; return false; }
                MusicStation.SpawnMarker(exec.Position);
                response = $"已放置点歌台（共 {MusicStation.StationCount} 个）\n坐标: {exec.Position.x:F2}, {exec.Position.y:F2}, {exec.Position.z:F2}";
                return true;
            }

            if (sub == "remove")
            {
                MusicStation.ClearMarkers();
                response = "已清除所有点歌台";
                return true;
            }

            if (sub == "stop")
            {
                MusicStation.ResetIntercomText();
                response = "已停止播放并还原广播栏";
                return true;
            }

            if (!int.TryParse(sub, out int idx))
            {
                response = "编号无效。用 music list 查看";
                return false;
            }

            int n = 0;
            foreach (var p in Player.List)
            {
                if (p == null || !p.IsConnected) continue;
                MusicStation.Play(p, idx);
                n++;
            }
            response = $"已播放（广播栏 + CASSIE 播报），影响 {n} 名玩家";
            return true;
        }
    }
}
