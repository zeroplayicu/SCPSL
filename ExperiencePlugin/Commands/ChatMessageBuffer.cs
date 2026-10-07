using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;

namespace ExperiencePlugin.Commands
{
    /// <summary>
    /// 聊天消息缓冲区 — BC支持倒计时，C支持堆叠显示
    /// </summary>
    public static class ChatMessageBuffer
    {
        private static readonly List<ChatEntry> _entries = new List<ChatEntry>();
        private static readonly object _lock = new object();
        private static DateTime _lastCountdownTick = DateTime.MinValue;

        private const int MaxMessages = 4;
        private const int MaxAgeSeconds = 30;

        /// <summary>bc_display 层最多同时显示的 BC 条数（避免多行向下延伸重叠）</summary>
        private const int MaxBcLines = 2;
        /// <summary>每条 BC 显示的最大字符数（防止超长消息换行造成重叠）</summary>
        private const int MaxBcLineLength = 40;
        /// <summary>teamchat_display 层最多同时显示的团队聊天条数</summary>
        private const int MaxTeamLines = 3;
        /// <summary>每条团队聊天显示的最大字符数</summary>
        private const int MaxTeamLineLength = 35;

        private class ChatEntry
        {
            public string FormattedText;
            public DateTime Time;
            public Team? Team;
            public int Countdown;
            public string PlayerName;
            public string RawMessage;
        }

        /// <summary>添加BC消息（带倒计时）</summary>
        public static void AddBc(string playerName, string message, int countdown)
        {
            lock (_lock)
            {
                _entries.Add(new ChatEntry
                {
                    PlayerName = playerName,
                    RawMessage = message,
                    Countdown = countdown,
                    Time = DateTime.Now,
                    Team = null
                });
                Cleanup();
            }
        }

        /// <summary>添加C消息（团队聊天）</summary>
        public static void AddC(string formattedText, Team team)
        {
            lock (_lock)
            {
                _entries.Add(new ChatEntry
                {
                    FormattedText = formattedText,
                    Time = DateTime.Now,
                    Team = team,
                    Countdown = 0
                });
                Cleanup();
            }
        }

        /// <summary>递减所有BC倒计时并清理到期的。</summary>
        /// <remarks>
        /// 该方法可能被 0.4s 的刷新计时器高频调用，因此内部用时间差保证
        /// 倒计时每 ~1 秒才减 1，避免 BC 消失速度过快。
        /// </remarks>
        public static void TickCountdowns()
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                if ((now - _lastCountdownTick).TotalSeconds < 1.0)
                    return;
                _lastCountdownTick = now;

                foreach (var e in _entries)
                {
                    if (e.Countdown > 0)
                        e.Countdown--;
                }
                _entries.RemoveAll(e => e.Countdown == 0 && e.Team == null);
            }
        }

        /// <summary>构建指定玩家可见的堆叠消息</summary>
        public static string BuildFor(Team? playerTeam)
        {
            lock (_lock)
            {
                Cleanup();
                var visible = _entries
                    .Where(e => e.Team == null || e.Team == playerTeam);
                var lines = new List<string>();
                foreach (var e in visible)
                {
                    if (e.Countdown > 0)
                        lines.Add($"【{e.Countdown}】{e.PlayerName}: {e.RawMessage}");
                    else
                        lines.Add(e.FormattedText);
                }
                return string.Join("\n", lines);
            }
        }

        /// <summary>构建BC消息（无倒计时的返回空）</summary>
        /// <remarks>
        /// 只取最新 2 条（按时间倒序），且每条截断到 MaxBcLineLength 字符，
        /// 避免多条/超长 BC 在 bc_display 层换行后与下方 teamchat_display 层重叠。
        /// </remarks>
        public static string BuildBcOnly()
        {
            lock (_lock)
            {
                var bcEntries = _entries
                    .Where(e => e.Team == null && e.Countdown > 0)
                    .OrderByDescending(e => e.Time)   // 最新在前
                    .Take(MaxBcLines);                // 最多显示 MaxBcLines 条
                var lines = new List<string>();
                foreach (var e in bcEntries)
                {
                    string msg = e.RawMessage;
                    if (msg.Length > MaxBcLineLength)
                        msg = msg.Substring(0, MaxBcLineLength) + "…";
                    // 去掉消息内的换行符，强制单行
                    msg = msg.Replace("\r", " ").Replace("\n", " ");
                    lines.Add($"【{e.Countdown}】{e.PlayerName}: {msg}");
                }
                return string.Join("\n", lines);
            }
        }

        /// <summary>构建指定玩家可见的团队聊天（C）消息</summary>
        /// <remarks>按时间倒序只取最新 MaxTeamLines 条，每条单行限长，避免多行向下延伸与战斗层重叠。</remarks>
        public static string BuildTeamMessages(Team? playerTeam)
        {
            lock (_lock)
            {
                Cleanup();
                var visible = _entries
                    .Where(e => e.Team != null && e.Team == playerTeam)
                    .OrderByDescending(e => e.Time)
                    .Take(MaxTeamLines);
                var lines = new List<string>();
                foreach (var e in visible)
                {
                    string line = e.FormattedText;
                    if (line.Length > MaxTeamLineLength)
                        line = line.Substring(0, MaxTeamLineLength) + "…";
                    line = line.Replace("\r", " ").Replace("\n", " ");
                    lines.Add(line);
                }
                return string.Join("\n", lines);
            }
        }

        private static void Cleanup()
        {
            var cutoff = DateTime.Now.AddSeconds(-MaxAgeSeconds);
            _entries.RemoveAll(e => e.Time < cutoff);
            while (_entries.Count > MaxMessages)
                _entries.RemoveAt(0);
        }
    }
}
