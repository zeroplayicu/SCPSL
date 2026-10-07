using System;
using System.Collections.Generic;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using MEC;
using RemoteAdmin;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// 3D 方块拼图命令（2026-10-06）：
    ///   tprim &lt;图名&gt; [帧号]    —— 在正前方渐进绘制（一行一行打印），并锁定玩家
    ///   tprim anim &lt;图名&gt; [fps]  —— 流畅播放全部帧
    ///   tprim lock [秒]         —— 只测试锁定玩家（不动 + 不转视角）
    ///   tprim stop              —— 清除所有方块 / 解除锁定
    ///   tprim list              —— 列出可用图
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class TestPrimitiveCommand : ICommand
    {
        public string Command => "tprim";
        public string[] Aliases => new[] { "primimage", "lifang" };
        public string Description => "3D 方块拼图：tprim <图名> [帧号] | tprim anim <图名> [fps] | tprim lock [秒] | tprim stop | tprim list";
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
                response = "用法: tprim <图名> [帧号] | tprim anim <图名> [fps] | tprim lock [秒] | tprim stop | tprim list";
                return false;
            }

            string sub = arguments.At(0).ToLowerInvariant();

            if (sub == "list")
            {
                try
                {
                    string root = PrimitiveImageRenderer.Root;
                    if (!System.IO.Directory.Exists(root)) { response = "图库目录不存在: " + root; return false; }
                    var dirs = System.IO.Directory.GetDirectories(root);
                    response = dirs.Length == 0 ? "暂无图片" : "可用: " + string.Join(", ", System.Array.ConvertAll(dirs, System.IO.Path.GetFileName));
                }
                catch (Exception ex) { response = "读取失败: " + ex.Message; }
                return true;
            }

            if (sub == "stop")
            {
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected) continue;
                    PrimitiveImageRenderer.Stop(p.UserId);
                    PrimitiveImageRenderer.UnlockPlayer(p);
                }
                PrimitiveImageRenderer.StopAll();
                response = "已清除所有 3D 方块并解除锁定";
                return true;
            }

            if (sub == "lock")
            {
                float sec = 10f;
                if (arguments.Count > 1 && float.TryParse(arguments.At(1), out float s2)) sec = Mathf.Clamp(s2, 1f, 120f);
                int n = 0;
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected || !p.IsAlive) continue;
                    PrimitiveImageRenderer.LockPlayer(p, sec);
                    n++;
                }
                response = $"已锁定 {n} 名玩家 {sec} 秒（不能移动、不能转视角）";
                return true;
            }

            // anim：流畅播放全帧
            if (sub == "anim")
            {
                if (arguments.Count < 2) { response = "用法: tprim anim <图名> [fps]"; return false; }
                string img = arguments.At(1);
                float fps = 10f;
                if (arguments.Count > 2 && float.TryParse(arguments.At(2), out float f2)) fps = Mathf.Clamp(f2, 1f, 30f);

                int frames = PrimitiveImageRenderer.FrameCount(img);
                if (frames <= 0) { response = $"找不到图片 '{img}'"; return false; }

                int n = 0;
                foreach (var p in Player.List)
                {
                    if (p == null || !p.IsConnected || !p.IsAlive) continue;
                    Timing.RunCoroutine(AnimRoutine(p, img, frames, fps));
                    n++;
                }
                response = $"播放 '{img}' 共 {frames} 帧 @ {fps}fps，发给 {n} 名玩家";
                return true;
            }

            // 默认：渐进绘制单帧 + 锁人
            string imageName = sub;
            int total = PrimitiveImageRenderer.FrameCount(imageName);
            if (total <= 0) { response = $"找不到图片 '{imageName}'（或没有 .grid 数据）"; return false; }

            int frame = total - 1;     // 默认播最后一帧（完整图案）
            if (arguments.Count > 1 && int.TryParse(arguments.At(1), out int fi)) frame = Mathf.Clamp(fi, 0, total - 1);

            int cnt = 0;
            foreach (var p in Player.List)
            {
                if (p == null || !p.IsConnected || !p.IsAlive || p.CameraTransform == null) continue;

                Vector3 origin = p.Position + p.CameraTransform.forward * 4f + Vector3.up * 0.5f;
                Quaternion rot = p.CameraTransform.rotation;

                PrimitiveImageRenderer.PlayProgressive(p, imageName, frame, origin, rot,
                    size: 0.1f, lineDelay: 0.07f, lockPlayer: true, holdSeconds: 8f);
                cnt++;
            }
            response = $"正在渐进绘制 '{imageName}' 第 {frame} 帧（{cnt} 名玩家，已锁定）";
            return true;
        }

        private static IEnumerator<float> AnimRoutine(Player player, string imageName, int frames, float fps)
        {
            Vector3 origin = player.Position + player.CameraTransform.forward * 4f + Vector3.up * 0.5f;
            Quaternion rot = player.CameraTransform.rotation;

            for (int rep = 0; rep < 2; rep++)
            {
                for (int f = 0; f < frames; f++)
                {
                    if (player == null || !player.IsConnected) yield break;
                    PrimitiveImageRenderer.SpawnFrame(player.UserId, imageName, f, origin, rot, 0.1f);
                    yield return Timing.WaitForSeconds(1f / fps);
                }
            }
            PrimitiveImageRenderer.Clear(player.UserId);
            player.ShowHint("<color=#88FF88>[3D图] 播放结束</color>", 2f);
        }
    }
}
