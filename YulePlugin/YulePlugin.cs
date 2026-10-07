using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;

namespace YulePlugin
{
    public class YulePlugin : Plugin<YuleConfig>
    {
        public static YulePlugin Instance { get; private set; }

        public override string Name => "YulePlugin";
        public override string Author => "Developer";
        public override string Prefix => "yule";

        /// <summary>autojc 开关：新加入的玩家自动变为教程角色</summary>
        public bool AutoTutorial { get; set; }

        /// <summary>自动教程开启时已处理的玩家（避免重复设置角色）</summary>
        private readonly System.Collections.Generic.HashSet<string> _handled = new System.Collections.Generic.HashSet<string>();

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            // 初始化 autojc 状态（默认按配置）
            AutoTutorial = Config.AutoTutorialDefaultOn;

            // 注册事件：玩家验证(加入)时若 autojc 开启则变教程角色
            Exiled.Events.Handlers.Player.Verified += OnPlayerVerified;
            // 玩家离开时清除去重记录，避免重连/下一局后 _handled 命中导致自动教程失效
            Exiled.Events.Handlers.Player.Left += OnPlayerLeft;

            Log.Info($"{Name} 加载完成 — 娱乐模式指令可用 (autojc: {(AutoTutorial ? "开" : "关")})");

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Verified -= OnPlayerVerified;
            Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;

            _handled.Clear();
            Instance = null;
            base.OnDisabled();
        }

        /// <summary>玩家离开时移除去重记录，保证重连/下一局后仍能自动变教程角色（修复"只有第一次进服才生效"）</summary>
        private void OnPlayerLeft(LeftEventArgs ev)
        {
            try
            {
                if (ev.Player == null || string.IsNullOrEmpty(ev.Player.UserId)) return;
                _handled.Remove(ev.Player.UserId);
            }
            catch { }
        }

        private void OnPlayerVerified(VerifiedEventArgs ev)
        {
            if (!AutoTutorial) return;
            if (ev.Player == null) return;

            try
            {
                // 延迟一帧设置角色，确保玩家已完全验证
                MEC.Timing.CallDelayed(0.5f, () =>
                {
                    try
                    {
                        if (ev.Player == null || !ev.Player.IsConnected) return;
                        // 防止重复设置：每个玩家只处理一次
                        if (!_handled.Add(ev.Player.UserId)) return;

                        ev.Player.Role.Set(RoleTypeId.Tutorial, Exiled.API.Enums.SpawnReason.Respawn, RoleSpawnFlags.All);
                        Log.Info($"[Yule] autojc: {ev.Player.Nickname} 已自动变为教程角色");
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"[Yule] autojc 设置角色失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[Yule] 处理玩家加入失败: {ex.Message}");
            }
        }

        /// <summary>判断游戏内玩家是否有管理员权限（AdminTools lv3+ 或远程管理权限）</summary>
        public bool IsAdmin(Player player)
        {
            try
            {
                if (player == null || player.ReferenceHub == null) return false;
                // 有远程管理权限即可
                if (player.ReferenceHub.serverRoles.RemoteAdmin) return true;
                return false;
            }
            catch { return false; }
        }

        /// <summary>当前娱乐模式状态文本</summary>
        public string GetStatusText()
        {
            return "══ 娱乐模式 ══\n" +
                   $"autojc (自动教程): {(AutoTutorial ? "<color=#44FF88>开启</color>" : "<color=#FF4444>关闭</color>")}";
        }
    }
}
