using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;

namespace ChatPlugin.Commands
{
    /// <summary>
    /// 聊天消息缓冲区 — 支持多人消息堆叠显示（新消息在下、旧消息在上）
    /// </summary>
    public static class ChatMessageBuffer
    {
        private static readonly List<ChatEntry> _entries = new List<ChatEntry>();
        private static readonly object _lock = new object();

        /// <summary>最大保留消息条数</summary>
        private const int MaxMessages = 8;

        /// <summary>消息最长存活秒数</summary>
        private const int MaxAgeSeconds = 30;

        private class ChatEntry
        {
            public string FormattedText;
            public DateTime Time;
            public Team? Team;
        }

        /// <summary>添加一条消息到缓冲区</summary>
        public static void Add(string text, Team? team = null)
        {
            lock (_lock)
            {
                _entries.Add(new ChatEntry
                {
                    FormattedText = text,
                    Time = DateTime.Now,
                    Team = team
                });
                Cleanup();
            }
        }

        /// <summary>构建指定玩家可见的堆叠消息（旧在上，新在下）</summary>
        public static string BuildFor(Team? playerTeam)
        {
            lock (_lock)
            {
                Cleanup();
                var visible = _entries
                    .Where(e => e.Team == null || e.Team == playerTeam)
                    .Select(e => e.FormattedText);
                return string.Join("\n", visible);
            }
        }

        /// <summary>清理过期和超出上限的消息</summary>
        private static void Cleanup()
        {
            var cutoff = DateTime.Now.AddSeconds(-MaxAgeSeconds);
            _entries.RemoveAll(e => e.Time < cutoff);
            while (_entries.Count > MaxMessages)
                _entries.RemoveAt(0);
        }
    }
}
