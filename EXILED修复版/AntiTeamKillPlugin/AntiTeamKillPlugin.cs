using System;
using System.Collections.Generic;
using System.Timers;
using Exiled.API.Features;
using MEC;

namespace AntiTeamKillPlugin
{
    public class AntiTeamKillPlugin : Plugin<AntiTeamKillConfig>
    {
        public static AntiTeamKillPlugin Instance { get; private set; }
        public AntiTeamKillEventHandler EventHandler { get; private set; }

        private Timer _saveTimer; // BUG-26修复: 警告数据延迟落盘
        // BUG-03同类修复: 用 MEC 协程句柄替代 System.Timers.Timer，
        // 保证在 Unity 主线程执行 Hint/Broadcast，避免跨线程操作游戏对象崩溃
        private MEC.CoroutineHandle _adminDisplayCoroutine;
        private bool _adminDisplayRunning;

        public override string Name => "AntiTeamKillPlugin";
        public override string Author => "Developer";
        public override string Prefix => "atk";

        public override void OnEnabled()
        {
            Instance = this;
            EventHandler = new AntiTeamKillEventHandler();

            Exiled.Events.Handlers.Player.Hurt += EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died += EventHandler.OnDied;
            // BUG-07修复: 玩家重生时清除其击杀记录，避免复活后仍能用旧数据开庭/变教程
            Exiled.Events.Handlers.Player.Spawned += EventHandler.OnSpawned;
            Exiled.Events.Handlers.Server.RoundStarted += EventHandler.OnRoundStarted;
            // BUG-26修复: 回合结束时统一落盘警告数据
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;

            // 管理员消息显示刷新：改用 MEC 主线程循环（每8秒一次）
            _adminDisplayRunning = true;
            _adminDisplayCoroutine = MEC.Timing.RunCoroutine(AdminDisplayLoop());

            // BUG-26修复: 定期保存警告数据（30秒一次），避免每次加警告都写盘
            _saveTimer = new Timer(30000);
            _saveTimer.Elapsed += (_, _) => EventHandler.FlushWarningsIfDirty();
            _saveTimer.AutoReset = true;
            _saveTimer.Start();

            Log.Info($"{Name} v{Version} 加载完成 - 反组杀/警告系统/.AC管理沟通");

            base.OnEnabled();
        }

        /// <summary>管理员消息显示刷新循环（MEC 主线程，每8秒刷新一次）</summary>
        private IEnumerator<float> AdminDisplayLoop()
        {
            while (_adminDisplayRunning)
            {
                yield return MEC.Timing.WaitForSeconds(8f);
                if (!_adminDisplayRunning) break;
                try
                {
                    if (EventHandler != null)
                        EventHandler.RefreshAdminDisplay();
                }
                catch (Exception ex) { Log.Error($"[反组杀] 管理员消息刷新失败: {ex.Message}"); }
            }
        }

        public override void OnDisabled()
        {
            // 先停止循环标记，再杀掉协程，避免协程在锁上残留
            _adminDisplayRunning = false;
            if (_adminDisplayCoroutine.IsRunning)
                MEC.Timing.KillCoroutines(_adminDisplayCoroutine);

            _saveTimer?.Stop();
            _saveTimer?.Dispose();

            Exiled.Events.Handlers.Player.Hurt -= EventHandler.OnHurt;
            Exiled.Events.Handlers.Player.Died -= EventHandler.OnDied;
            Exiled.Events.Handlers.Player.Spawned -= EventHandler.OnSpawned;
            Exiled.Events.Handlers.Server.RoundStarted -= EventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;

            EventHandler?.SaveAllData();
            EventHandler = null;
            Instance = null;

            base.OnDisabled();
        }

        /// <summary>BUG-26修复: 回合结束时落盘警告数据</summary>
        private void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            EventHandler?.FlushWarningsIfDirty();
        }
    }
}
