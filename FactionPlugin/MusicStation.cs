using System;
using System.Collections.Generic;
using System.Reflection;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// 「点歌台」（2026-10-06）。
    ///
    /// ⚠️ 重要限制（实测结论）：
    ///   SCP:SL **服务端无法播放自定义音频**（mp3 / 网上下载的音乐都不行）——
    ///   音频资源随客户端分发，服务端只能触发**游戏内置音频库**（枪声/门/警报/CASSIE）。
    ///   想要真正"放歌"必须做客户端 Mod。
    ///
    /// 因此本模块用**可行的组合**做出"点歌台"体验：
    ///   1. 覆盖 **intercom（广播栏）的显示文字** → 绿条上显示「♪ 正在播放：歌名」✓
    ///   2. **CASSIE 语音播报** → 全服能"听到"播报（这是服务端唯一能发声的通道）✓
    ///   3. 可选：把玩家叫到点歌台位置播放（触发提示音）
    /// </summary>
    public static class MusicStation
    {
        private static readonly Dictionary<string, CoroutineHandle> Playing = new Dictionary<string, CoroutineHandle>();

        // ===== 点歌台实体（2026-10-06）=====
        private static readonly List<Primitive> Markers = new List<Primitive>();
        private static readonly List<Vector3> Stations = new List<Vector3>();
        private static readonly Dictionary<string, DateTime> LastTrigger = new Dictionary<string, DateTime>();
        private const float TriggerRadius = 3f;
        private const float TriggerCooldown = 25f;

        /// <summary>在指定位置生成点歌台标记（青色发光柱）</summary>
        public static void SpawnMarker(Vector3 pos)
        {
            try
            {
                var m = Primitive.Create(PrimitiveType.Cylinder);
                m.Position = pos + Vector3.up * 1.2f;
                m.Rotation = Quaternion.identity;
                m.Scale = new Vector3(0.8f, 1.2f, 0.8f);
                m.Color = new Color(0.2f, 1f, 0.65f, 1f);
                m.Flags = PrimitiveFlags.Visible;
                m.IsStatic = true;
                try { m.Spawn(); } catch { }
                Markers.Add(m);
                Stations.Add(pos);
                Log.Info($"[点歌台] 已放置 @ {pos.x:F2}, {pos.y:F2}, {pos.z:F2}");
            }
            catch (Exception ex) { Log.Warn($"[点歌台] 放置标记失败: {ex.Message}"); }
        }

        /// <summary>清除所有点歌台</summary>
        public static void ClearMarkers()
        {
            foreach (var m in Markers) { try { m?.Destroy(); } catch { } }
            Markers.Clear();
            Stations.Clear();
            Log.Info("[点歌台] 已清除所有点歌台");
        }

        public static int StationCount => Stations.Count;

        /// <summary>玩家靠近检测（由主协程驱动）</summary>
        public static void Tick()
        {
            if (Stations.Count == 0) return;
            try
            {
                foreach (var player in Player.List)
                {
                    if (player == null || !player.IsConnected || !player.IsAlive) continue;
                    foreach (var st in Stations)
                    {
                        if (Vector3.Distance(player.Position, st) > TriggerRadius) continue;

                        if (LastTrigger.TryGetValue(player.UserId, out DateTime t) &&
                            (DateTime.Now - t).TotalSeconds < TriggerCooldown) break;

                        LastTrigger[player.UserId] = DateTime.Now;
                        player.ShowHint("<color=#66FFCC>[点歌台]</color> 已为你点播", 3f);
                        Play(player, 0);
                        break;
                    }
                }
            }
            catch (Exception ex) { Log.Debug($"[点歌台] tick 异常: {ex.Message}"); }
        }

        public class Song
        {
            public string Name;
            public string Cassie;
            public string[] Lyrics;
        }

        /// <summary>热门曲目（2026-10-06）：服务端无法播真实音频，这里用「广播栏滚动歌词 + CASSIE 报幕」做演出</summary>
        public static class Songs
        {
            public static readonly Song[] List =
            {
                new Song
                {
                    Name = "孤勇者", Cassie = "NOW PLAYING TRACK ONE",
                    Lyrics = new[] { "爱你孤身走暗巷", "爱你不跪的模样", "爱你对峙过绝望", "不肯哭一场", "致那黑夜中的呜咽与怒吼" }
                },
                new Song
                {
                    Name = "起风了", Cassie = "NOW PLAYING TRACK TWO",
                    Lyrics = new[] { "这一路上走走停停", "顺着少年漂流的痕迹", "迈出车站的前一刻", "竟有些犹豫", "我曾难自拔于世界之大" }
                },
                new Song
                {
                    Name = "稻香", Cassie = "NOW PLAYING TRACK THREE",
                    Lyrics = new[] { "还记得你说家是唯一的城堡", "随着稻香河流继续奔跑", "微微笑 小时候的梦我知道", "不要哭 让萤火虫带着你逃跑", "乡间的歌谣永远的依靠" }
                },
                new Song
                {
                    Name = "夜曲", Cassie = "NOW PLAYING TRACK FOUR",
                    Lyrics = new[] { "为你弹奏萧邦的夜曲", "纪念我死去的爱情", "跟夜风一样的声音", "心碎的很好听", "手在键盘敲很轻" }
                },
                new Song
                {
                    Name = "平凡之路", Cassie = "NOW PLAYING TRACK FIVE",
                    Lyrics = new[] { "我曾经跨过山和大海", "也穿过人山人海", "我曾经拥有着的一切", "转眼都飘散如烟", "直到看见平凡才是唯一的答案" }
                },
                new Song
                {
                    Name = "海阔天空", Cassie = "NOW PLAYING TRACK SIX",
                    Lyrics = new[] { "今天我 寒夜里看雪飘过", "怀着冷却了的心窝漂远方", "风雨里追赶 雾里分不清影踪", "原谅我这一生不羁放纵爱自由", "也会怕有一天会跌倒" }
                },
            };
        }

        /// <summary>播放一首"歌"（= intercom 显示 + CASSIE 播报）</summary>
        public static void Play(Player player, int index)
        {
            if (player == null) return;
            var list = Songs.List;
            if (index < 0 || index >= list.Length) return;


            var song = list[index];

            // CASSIE 报幕（服务端唯一发声通道）
            try { Exiled.API.Features.Cassie.Message(song.Cassie, false, false, false); } catch (Exception ex) { Log.Debug($"[点歌台] CASSIE 失败: {ex.Message}"); }

            // 广播栏滚动歌词
            string key = player != null ? player.UserId : "global";
            if (Playing.TryGetValue(key, out var h)) Timing.KillCoroutines(h);
            Playing[key] = Timing.RunCoroutine(LyricRoutine(key, song));

            player.ShowHint($"<color=#88FF88>[点歌台]</color> 正在播放：<color=#FFD700>{song.Name}</color>", 5f);
            Log.Info($"[点歌台] {player.Nickname} 点了「{song.Name}」");

            // 全服提示歌词演出
            Map.Broadcast(4, $"<color=#88FF88>[点歌台]</color> <color=#FFD700>{song.Name}</color> · 正在演唱（看广播栏）");
        }

        /// <summary>歌词逐行滚动（广播栏）</summary>
        private static IEnumerator<float> LyricRoutine(string key, Song song)
        {
            for (int i = 0; i < song.Lyrics.Length; i++)
            {
                SetIntercomText($"♪ {song.Name} · {i + 1}/{song.Lyrics.Length} ♪\n{song.Lyrics[i]}");
                yield return Timing.WaitForSeconds(3.4f);
            }
            SetIntercomText($"♪ {song.Name} ♪\n—— 演唱完毕 ——");
            yield return Timing.WaitForSeconds(3f);
            ResetIntercomText();
            Playing.Remove(key);
        }

        private static int _nextIndex = -1;

        /// <summary>E 交互触发：玩家在点歌台附近做交互时，切下一首（2026-10-06）</summary>
        public static bool TryInteract(Player player)
        {
            if (player == null || Stations.Count == 0) return false;
            try
            {
                foreach (var st in Stations)
                {
                    if (UnityEngine.Vector3.Distance(player.Position, st) > TriggerRadius) continue;
                    _nextIndex = (_nextIndex + 1) % Songs.List.Length;
                    Play(player, _nextIndex);
                    player.ShowHint($"<color=#66FFCC>[点歌台]</color> 切歌 → <color=#FFD700>{Songs.List[_nextIndex].Name}</color>", 3f);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>列出可点歌曲名（给命令用）</summary>
        public static string ListSongs()
        {
            var sb = new System.Text.StringBuilder();
            var list = Songs.List;
            for (int i = 0; i < list.Length; i++)
                sb.Append($"{i}:{list[i].Name}  ");
            return sb.ToString();
        }

        /// <summary>设置 intercom 广播栏显示文字（用反射调游戏 API，失败不影响主流程）</summary>
        public static void SetIntercomText(string text, Player onlyFor = null)
        {
            try
            {
                Assembly gameAsm = typeof(ServerRoles).Assembly;

                // 候选类型名（不同版本可能不同）
                string[] typeNames = { "IntercomDisplay", "Intercom", "IntercomDisplayController" };
                foreach (var tn in typeNames)
                {
                    Type type = gameAsm.GetType(tn);
                    if (type == null) continue;

                    string[] methodNames = { "SetIntercomDisplayTextForTargetOnly", "SetIntercomDisplayText", "RpcSetDisplayText" };
                    foreach (var mn in methodNames)
                    {
                        foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            if (mi.Name != mn) continue;
                            var ps = mi.GetParameters();
                            object instance = null;
                            try
                            {
                                if (!mi.IsStatic) instance = UnityEngine.Object.FindObjectOfType(type);
                                if (!mi.IsStatic && instance == null) continue;

                                object[] args = BuildArgs(ps, text, onlyFor);
                                if (args == null) continue;

                                mi.Invoke(instance, args);
                                Log.Debug($"[点歌台] intercom 文字已设置（{tn}.{mn}）");
                                return;
                            }
                            catch { /* 换下一个 */ }
                        }
                    }
                }
                Log.Debug("[点歌台] 未找到可用的 intercom 显示 API（不影响播放）");
            }
            catch (Exception ex) { Log.Debug($"[点歌台] intercom 显示异常: {ex.Message}"); }
        }

        private static object[] BuildArgs(ParameterInfo[] ps, string text, Player onlyFor)
        {
            try
            {
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    Type t = ps[i].ParameterType;
                    if (t == typeof(string)) args[i] = text;
                    else if (t == typeof(bool)) args[i] = false;
                    else if (t == typeof(float)) args[i] = 8f;
                    else if (t == typeof(int)) args[i] = 8;
                    else if (onlyFor != null && t.IsAssignableFrom(typeof(ReferenceHub))) args[i] = onlyFor.ReferenceHub;
                    else if (t.IsValueType) args[i] = Activator.CreateInstance(t);
                    else return null;   // 未知引用类型 → 放弃这个重载
                }
                return args;
            }
            catch { return null; }
        }

        /// <summary>把广播栏文字还原</summary>
        public static void ResetIntercomText()
        {
            try
            {
                Assembly gameAsm = typeof(ServerRoles).Assembly;
                Type type = gameAsm.GetType("IntercomDisplay");
                var mi = type?.GetMethod("ResetIntercomDisplayText", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                if (mi != null)
                {
                    object inst = mi.IsStatic ? null : UnityEngine.Object.FindObjectOfType(type);
                    if (mi.IsStatic || inst != null) mi.Invoke(inst, null);
                }
            }
            catch { }
        }
    }
}
