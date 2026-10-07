using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using MEC;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace FactionPlugin
{
    /// <summary>
    /// 图片/动画 HUD 渲染器（2026-10-06）。
    ///
    /// 思路：SCP:SL 的 HUD 支持彩色富文本，把图片降采样成方格后，
    /// 每个方格输出一个方块字符（█）+ 颜色标签，即为"字符画"。
    /// 单个 hint 有长度上限，所以把整图按 N×N 切块，**每块一个 hint**，
    /// 按象限定位拼成大图，从而获得更高的等效分辨率（高清）。
    ///
    /// 数据由 D:/workbuddy/SCPSL pulint/.tools/img2hint.py 离线生成，
    /// 放在 EXILED/Configs/plugins/faction_plugin/images/&lt;图名&gt;/&lt;帧号:000&gt;_&lt;块号&gt;.txt
    /// </summary>
    public static class ImageHudRenderer
    {
        private const string LayerPrefix = "imghud";

        public static string ImagesRoot =>
            Path.Combine(Paths.Plugins, "FactionPluginImages");

        private static readonly Dictionary<string, string[]> LastBlockCache = new Dictionary<string, string[]>();

        /// <summary>显示某一帧（分块拼接）</summary>
        public static void ShowFrame(Player player, string imageName, int frameIndex,
            int blockN = 4, int blockCols = 24, int blockRows = 12,
            byte fontSize = 13, int centerY = 420)
        {
            try
            {
                if (player == null || !player.IsConnected) return;
                string dir = Path.Combine(ImagesRoot, imageName);

                // 估算像素尺寸
                float charW = fontSize * 0.62f;      // 方块字符宽 ≈ 0.62×字号
                float lineH = fontSize * 1.25f;      // 行高
                float blockW = blockCols * charW;
                float blockH = blockRows * lineH;
                float totalW = blockN * blockW;
                float totalH = blockN * blockH;

                var disp = PlayerDisplay.Get(player);
                string cacheKeyBase = $"{player.UserId}|{imageName}";
                if (!LastBlockCache.TryGetValue(cacheKeyBase, out string[] last))
                {
                    last = new string[blockN * blockN];
                    LastBlockCache[cacheKeyBase] = last;
                }

                for (int by = 0; by < blockN; by++)
                {
                    for (int bx = 0; bx < blockN; bx++)
                    {
                        int idx = by * blockN + bx;
                        string path = Path.Combine(dir, $"{frameIndex:D3}_{idx}.txt");
                        if (!File.Exists(path)) continue;

                        string txt = File.ReadAllText(path, System.Text.Encoding.UTF8);
                        if (last[idx] == txt) continue;      // 内容没变就不重推，减少闪烁与开销

                        last[idx] = txt;
                        string layerId = $"{LayerPrefix}_{idx}";
                        disp.RemoveHint(layerId);

                        // 块中心坐标（HSM：X 相对屏幕中心，Y 从上往下）
                        float cx = -totalW / 2f + blockW * bx + blockW / 2f;
                        float cy = centerY - totalH / 2f + blockH * by + blockH / 2f;

                        disp.ShowHint(new HsmHint
                        {
                            Id = layerId,
                            Text = txt,
                            FontSize = fontSize,
                            XCoordinate = cx,
                            YCoordinate = cy,
                            Alignment = HintAlignment.Center
                        }, 30f);
                    }
                }
            }
            catch (Exception ex) { Log.Debug($"[图片HUD] 显示失败: {ex.Message}"); }
        }

        /// <summary>播放动画（协程，主线程）</summary>
        public static void Play(Player player, string imageName, float fps = 5f, int repeat = 1,
            int blockN = 4, int blockCols = 24, int blockRows = 12,
            byte fontSize = 13, int centerY = 420)
        {
            Timing.RunCoroutine(PlayRoutine(player, imageName, fps, repeat, blockN, blockCols, blockRows, fontSize, centerY));
        }

        private static IEnumerator<float> PlayRoutine(Player player, string imageName, float fps, int repeat,
            int blockN, int blockCols, int blockRows, byte fontSize, int centerY)
        {
            int frames = FrameCount(imageName);
            if (frames <= 0) yield break;

            for (int rep = 0; rep < repeat; rep++)
            {
                for (int f = 0; f < frames; f++)
                {
                    if (player == null || !player.IsConnected) yield break;
                    ShowFrame(player, imageName, f, blockN, blockCols, blockRows, fontSize, centerY);
                    yield return Timing.WaitForSeconds(1f / Math.Max(0.5f, fps));
                }
            }

            if (repeat > 0 && player != null && player.IsConnected)
            {
                // 播完保留最后一帧（不立即清除，方便观察）
                yield return Timing.WaitForSeconds(1.5f);
                Stop(player);
            }
        }

        public static int FrameCount(string imageName)
        {
            try
            {
                string dir = Path.Combine(ImagesRoot, imageName);
                if (!Directory.Exists(dir)) return 0;
                return Directory.GetFiles(dir, "*_0.txt").Length;
            }
            catch { return 0; }
        }

        public static void Stop(Player player)
        {
            try
            {
                if (player == null) return;
                var disp = PlayerDisplay.Get(player);
                for (int i = 0; i < 16; i++) disp.RemoveHint($"{LayerPrefix}_{i}");
                LastBlockCache.Remove($"{player.UserId}");
                foreach (var k in LastBlockCache.Keys.Where(k => k.StartsWith(player.UserId)).ToList())
                    LastBlockCache.Remove(k);
            }
            catch { }
        }
    }
}
