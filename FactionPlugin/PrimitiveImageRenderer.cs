using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using AdminToys;
using MEC;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// 3D 方块拼图渲染器（2026-10-06）。
    ///
    /// 为什么不用 HUD 字符画：游戏字体不支持方块字符（`timg font` 实测一个都显示不出来）。
    /// 改用 **PrimitiveObjectToy（原生物体玩具）** 在 3D 世界里逐格生成彩色方块，
    /// 这样清晰度不受文本长度限制，而且能被场景物体正常遮挡，视觉上是"真·立体像素画"。
    ///
    /// 数据：EXILED/Plugins/FactionPluginImages/&lt;图名&gt;/&lt;帧号:000&gt;.grid
    ///   第 1 行 = 调色板（5 个 hex，逗号分隔）
    ///   之后每行 = 一行像素，'0' 透明，'1'-'5' 为调色板索引
    /// </summary>
    public static class PrimitiveImageRenderer
    {
        private static readonly Dictionary<string, List<Primitive>> Spawned = new Dictionary<string, List<Primitive>>();
        private static readonly Dictionary<string, CoroutineHandle> Handles = new Dictionary<string, CoroutineHandle>();

        /// <summary>图片数据目录：**按实例端口隔离**（Users 要求 2026-10-06：不放根目录，只放 7779/7780）</summary>
        public static string Root => Path.Combine(Paths.Plugins, Server.Port.ToString(), "FactionPluginImages");

        // ===== 单帧生成（一次性）=====
        public static int SpawnFrame(string key, string imageName, int frameIndex,
            Vector3 origin, Quaternion rot, float size = 0.12f, float lineDelay = 0f)
        {
            Clear(key);
            var list = new List<Primitive>();
            Spawned[key] = list;

            if (!LoadGrid(imageName, frameIndex, out string[] palette, out List<string> rows))
                return 0;

            int h = rows.Count;
            int w = rows.Max(r => r.Length);

            for (int y = 0; y < h; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c == '0') continue;
                    int pi = c - '1';
                    if (pi < 0 || pi >= palette.Length) continue;

                    if (!ColorUtility.TryParseHtmlString("#" + palette[pi], out Color col)) continue;

                    // 以 origin 为图像中心，Y 轴向下（与数据行序一致）
                    Vector3 pos = origin + rot * new Vector3(
                        (x - w / 2f) * size,
                        -(y - h / 2f) * size,
                        0f);

                    try
                    {
                        var p = Primitive.Create(PrimitiveType.Cube);
                        p.Position = pos;
                        p.Rotation = rot;
                        p.Scale = new Vector3(size, size, size);
                        p.Color = col;
                        p.Flags = PrimitiveFlags.Visible;   // 可见但不可碰撞（不挡玩家）
                        p.IsStatic = true;
                        list.Add(p);
                    }
                    catch (Exception ex)
                    {
                        if (list.Count == 0) Log.Warn($"[3D图] 创建方块失败: {ex.Message}");
                    }
                }
            }
            return list.Count;
        }

        // ===== 渐进绘制：一行一行打印出来 =====
        public static void PlayProgressive(Player viewer, string imageName, int frameIndex,
            Vector3 origin, Quaternion rot, float size = 0.12f, float lineDelay = 0.09f,
            bool lockPlayer = true, float holdSeconds = 3f)
        {
            string key = viewer != null ? viewer.UserId : "global";
            Timing.KillCoroutines(Handles.TryGetValue(key, out var h) ? h : default);
            Handles[key] = Timing.RunCoroutine(ProgressiveRoutine(viewer, key, imageName, frameIndex,
                origin, rot, size, lineDelay, lockPlayer, holdSeconds));
        }

        private static IEnumerator<float> ProgressiveRoutine(Player viewer, string key, string imageName,
            int frameIndex, Vector3 origin, Quaternion rot, float size, float lineDelay,
            bool lockPlayer, float holdSeconds)
        {
            Clear(key);
            var list = new List<Primitive>();
            Spawned[key] = list;

            if (!LoadGrid(imageName, frameIndex, out string[] palette, out List<string> rows))
            {
                viewer?.ShowHint("<color=#FF4444>[3D图] 找不到数据文件</color>", 4f);
                yield break;
            }

            int h = rows.Count;
            int w = rows.Max(r => r.Length);
            Log.Info($"[3D图] 开始渐进绘制 '{imageName}' 第 {frameIndex} 帧（{w}x{h} 格，size={size}）");

            Vector3 lockPos = viewer?.Position ?? Vector3.zero;
            Quaternion lockRot = viewer?.CameraTransform != null ? viewer.CameraTransform.rotation : Quaternion.identity;
            float elapsed = 0f;

            // 渐进：一行一行刷出来
            for (int y = 0; y < h; y++)
            {
                if (viewer != null && !viewer.IsConnected) break;

                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c == '0') continue;
                    int pi = c - '1';
                    if (pi < 0 || pi >= palette.Length) continue;
                    if (!ColorUtility.TryParseHtmlString("#" + palette[pi], out Color col)) continue;

                    Vector3 pos = origin + rot * new Vector3(
                        (x - w / 2f) * size,
                        -(y - h / 2f) * size,
                        0f);

                    try
                    {
                        var pb = Primitive.Create(PrimitiveType.Cube);
                        pb.Position = pos;
                        pb.Rotation = rot;
                        pb.Scale = new Vector3(size, size, size);
                        pb.Color = col;
                        pb.Flags = PrimitiveFlags.Visible;
                        pb.IsStatic = true;
                        list.Add(pb);
                    }
                    catch { }
                }

                // 锁定玩家：位置 + 视角都钉住（Ensnared 只管移动，视角要自己写）
                if (lockPlayer && viewer != null && viewer.IsConnected)
                {
                    try
                    {
                        viewer.Position = lockPos;
                        if (viewer.CameraTransform != null)
                            viewer.CameraTransform.rotation = lockRot;
                    }
                    catch { }
                }

                yield return Timing.WaitForSeconds(lineDelay);
                elapsed += lineDelay;
            }

            Log.Info($"[3D图] '{imageName}' 第 {frameIndex} 帧渐进绘制完成，{list.Count} 个方块");

            // 保持显示，期间持续锁人
            float hold = 0f;
            while (hold < holdSeconds)
            {
                if (viewer != null && !viewer.IsConnected) yield break;
                if (lockPlayer && viewer != null)
                {
                    try
                    {
                        viewer.Position = lockPos;
                        if (viewer.CameraTransform != null) viewer.CameraTransform.rotation = lockRot;
                    }
                    catch { }
                }
                yield return Timing.WaitForSeconds(0.05f);
                hold += 0.05f;
            }
        }

        // ===== 读数据 =====
        private static bool LoadGrid(string imageName, int frameIndex,
            out string[] palette, out List<string> rows)
        {
            palette = null; rows = null;
            try
            {
                string path = Path.Combine(Root, imageName, $"{frameIndex:D3}.grid");
                if (!File.Exists(path)) return false;

                string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
                if (lines.Length < 2) return false;

                palette = lines[0].Split(',');
                rows = new List<string>();
                for (int i = 1; i < lines.Length; i++)
                    rows.Add(lines[i]);
                return true;
            }
            catch (Exception ex) { Log.Debug($"[3D图] 读取失败: {ex.Message}"); return false; }
        }

        public static int FrameCount(string imageName)
        {
            try
            {
                string dir = Path.Combine(Root, imageName);
                return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.grid").Length : 0;
            }
            catch { return 0; }
        }

        // ===== 清理 =====
        public static void Clear(string key)
        {
            try
            {
                if (Spawned.TryGetValue(key, out var list))
                {
                    foreach (var p in list)
                    {
                        try { p?.Destroy(); } catch { }
                    }
                    list.Clear();
                }
            }
            catch { }
        }

        public static void Stop(string key)
        {
            if (Handles.TryGetValue(key, out var h)) { Timing.KillCoroutines(h); Handles.Remove(key); }
            Clear(key);
        }

        public static void StopAll()
        {
            foreach (var k in Handles.Keys.ToList()) Timing.KillCoroutines(Handles[k]);
            Handles.Clear();
            foreach (var k in Spawned.Keys.ToList()) Clear(k);
        }

        /// <summary>把玩家锁在原地（不能移动 + 不能转视角），持续 seconds 秒</summary>
        public static void LockPlayer(Player player, float seconds)
        {
            if (player == null || !player.IsConnected) return;
            Vector3 pos = player.Position;
            Quaternion rot = player.CameraTransform != null ? player.CameraTransform.rotation : Quaternion.identity;

            try { player.EnableEffect(EffectType.Ensnared, 1, seconds); } catch { }

            Timing.RunCoroutine(LockRoutine(player, pos, rot, seconds));
        }

        private static IEnumerator<float> LockRoutine(Player player, Vector3 pos, Quaternion rot, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (player == null || !player.IsConnected) yield break;
                try
                {
                    player.Position = pos;
                    if (player.CameraTransform != null) player.CameraTransform.rotation = rot;
                }
                catch { }
                yield return Timing.WaitForSeconds(0.03f);
                t += 0.03f;
            }
        }

        public static void UnlockPlayer(Player player)
        {
            try { player?.DisableEffect(EffectType.Ensnared); } catch { }
        }
    }
}
