using System;
using System.Linq;
using System.Collections.Generic;
using Exiled.API.Features;
using PlayerRoles;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace SpectatorListPlugin
{
    /// <summary>
    /// 观战列表显示核心：用 HSM 的 spectator_display 层，向开启了开关的玩家显示"正在观战自己的人"。
    /// 显示"被观战者 → 观战者列表"（通过 SpectatorTracker 维护）。
    /// 使用持久 Hint(60s)，只在文本变化时重发，避免闪烁。
    /// 注意：HSM 不处理 Text 里的内联 &lt;size&gt; 标签，字号用 FontSize 控制，Text 只保留 &lt;color&gt;。
    /// </summary>
    public class SpectatorDisplay
    {
        private readonly SpectatorListPlugin _plugin;
        private readonly Dictionary<string, string> _lastTexts = new Dictionary<string, string>();

        public SpectatorDisplay(SpectatorListPlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>刷新所有开启观战列表玩家的显示（只显示"谁在观战我"）</summary>
        public void RefreshAll()
        {
            try
            {
                foreach (var player in Player.List.Where(p => p != null && !p.IsNPC && !string.IsNullOrEmpty(p.UserId)))
                {
                    var data = _plugin.DataManager.Get(player.UserId);
                    if (data == null) continue;

                    // 两个开关都关着则不显示
                    if (!data.ShowSpectatorCount && !data.ShowSpectatorDetail)
                    {
                        TryShowHint(player, "spectator_display", "");
                        continue;
                    }

                    // 获取正在观战该玩家的观战者
                    var watchers = _plugin.Tracker.GetSpectatorsOf(player);

                    var sb = new System.Text.StringBuilder();

                    // 1. 观战人数(观战我的人数)，无论是否有人都显示(0 也显示)
                    if (data.ShowSpectatorCount)
                    {
                        string countLine = _plugin.Config.CountTemplate
                            .Replace("{count}", watchers.Count.ToString())
                            .Replace("{total}", watchers.Count.ToString());
                        sb.Append(countLine);
                    }

                    // 2. 详细名单(观战我的玩家名字)，无人时跳过详情
                    if (data.ShowSpectatorDetail && watchers.Count > 0)
                    {
                        if (sb.Length > 0)
                            sb.Append("\n");
                        sb.Append(_plugin.Config.DetailHeader);
                        foreach (var watcher in watchers)
                        {
                            sb.Append("\n");
                            string name = watcher.Nickname;
                            string nickname = watcher.DisplayNickname;
                            if (string.IsNullOrEmpty(nickname)) nickname = name;
                            sb.Append(_plugin.Config.DetailLineTemplate
                                .Replace("{name}", name)
                                .Replace("{nickname}", nickname));
                        }
                    }

                    string finalText = sb.ToString();
                    if (string.IsNullOrWhiteSpace(finalText))
                        finalText = " ";

                    TryShowHint(player, "spectator_display", finalText);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[观战列表] 刷新显示出错: {ex.Message}");
            }
        }

        private void TryShowHint(Player player, string layerId, string text)
        {
            string key = $"{player.UserId}|{layerId}";
            _lastTexts[key] = text;

            var hint = new HsmHint
            {
                Id = layerId,
                Text = text,
                FontSize = _plugin.Config.DisplayFontSize,
                YCoordinate = _plugin.Config.DisplayYCoordinate,
                Alignment = _plugin.Config.Alignment
            };
            // HSM 同 Id 不替换不续期：内容不变也不能跳过重发，否则静态文本 60 秒后到期消失、
            // 动态文本新旧实例叠加重影 —— 每次刷新都先 RemoveHint 同 Id 旧实例再 ShowHint
            var disp = PlayerDisplay.Get(player);
            disp.RemoveHint(layerId);
            disp.ShowHint(hint, 60f);
        }

        /// <summary>清空某玩家的观战列表显示（卸载/回合结束时可调用）</summary>
        public void Clear(Player player)
        {
            try
            {
                if (player == null) return;
                string key = $"{player.UserId}|spectator_display";
                _lastTexts.Remove(key);
                PlayerDisplay.Get(player).RemoveHint("spectator_display");
            }
            catch { }
        }

        public void ClearAllTexts()
        {
            _lastTexts.Clear();
        }
    }
}
