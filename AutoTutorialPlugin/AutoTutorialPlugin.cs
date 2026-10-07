using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;

namespace AutoTutorialPlugin
{
    public class AutoTutorialPlugin : Plugin<AutoTutorialConfig>
    {
        public static AutoTutorialPlugin Instance { get; private set; }

        public override string Name => "AutoTutorialPlugin";
        public override string Author => "Developer";
        public override string Prefix => "autotutorial";

        /// <summary>已处理的玩家(避免同一等待期内重复设置角色)。
        /// 每回合开始时清空，保证玩家下一局/重连后仍能被正常设为教程角色，
        /// 不会因历史去重而永久失效。</summary>
        private readonly HashSet<string> _handled = new HashSet<string>();

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            Exiled.Events.Handlers.Player.Verified += OnPlayerVerified;
            // 游戏分配角色前触发：把教程玩家改回观察者，交由游戏按正常规则分配角色（避免强制分配导致开局变九尾狐）
            Exiled.Events.Handlers.Server.ChoosingStartTeamQueue += OnChoosingStartTeamQueue;
            // 回合开始 -> 重置去重表，下一局玩家重新参与自动教程逻辑
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;

            Log.Info($"{Name} 加载完成 — 进入自动变教程角色，准备倒计时<{Config.SpectatorThreshold}秒时变观察者；回合开始前把教程角色交还游戏正常分配");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Verified -= OnPlayerVerified;
            Exiled.Events.Handlers.Server.ChoosingStartTeamQueue -= OnChoosingStartTeamQueue;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;

            _handled.Clear();
            Instance = null;
            base.OnDisabled();
        }

        /// <summary>
        /// 每回合开始重置去重表。
        /// 关键修复：之前 _handled 永久不清空，导致玩家断线重连/下一局进来后
        /// _handled.Add 返回 false 直接 return，自动变教程的功能"没过几把就失效"。
        /// 改为回合级去重：同一等待期内防重复，跨局/重连仍生效。
        /// </summary>
        private void OnRoundStarted()
        {
            _handled.Clear();
            Log.Info($"[AutoTutorial] 回合开始，已重置角色自动设置去重表");
        }

        /// <summary>
        /// 游戏开始分配角色前触发（RoleAssigner.OnRoundStarted）。
        /// 把仍是教程角色的玩家改回观察者，让游戏按正常规则分配角色，
        /// 使其能正常参加本局对局，避免卡教程角色，也避免强制随机分配导致开局直接变成九尾狐。
        /// </summary>
        private void OnChoosingStartTeamQueue(ChoosingStartTeamQueueEventArgs ev)
        {
            try
            {
                foreach (var player in Player.List)
                {
                    if (player == null || !player.IsConnected || player.IsNPC) continue;
                    if (player.Role.Type != RoleTypeId.Tutorial) continue;

                    player.Role.Set(RoleTypeId.Spectator, Exiled.API.Enums.SpawnReason.ForceClass, RoleSpawnFlags.All);
                    Log.Info($"[AutoTutorial] 回合开始分配前，{player.Nickname} 从教程角色变回观察者，交由游戏正常分配角色");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoTutorial] 回合开始交还角色分配失败: {ex.Message}");
            }
        }

        private void OnPlayerVerified(VerifiedEventArgs ev)
        {
            if (ev.Player == null) return;
            // 防止重复设置：每个玩家只处理一次
            if (!_handled.Add(ev.Player.UserId)) return;

            try
            {
                // 回合已在进行中：新进入的玩家直接设为观察者(观战)，避免被设为教程角色后整局卡住无法参加对局
                if (Round.IsStarted)
                {
                    ev.Player.Role.Set(RoleTypeId.Spectator, Exiled.API.Enums.SpawnReason.ForceClass, RoleSpawnFlags.All);
                    Log.Info($"[AutoTutorial] 回合已开始，{ev.Player.Nickname} 已自动变为观察者");
                    return;
                }

                // Verified 时玩家已进入等待大厅，直接设置角色。
                // 准备倒计时少于阈值 => 回合即将开始，改为观察者；
                // 否则 => 变为教程角色(回合开始分配前由 OnChoosingStartTeamQueue 交还游戏正常分配角色)。
                if (GetCountdownSeconds() < Config.SpectatorThreshold)
                {
                    ev.Player.Role.Set(RoleTypeId.Spectator, Exiled.API.Enums.SpawnReason.ForceClass, RoleSpawnFlags.All);
                    Log.Info($"[AutoTutorial] 准备倒计时<{Config.SpectatorThreshold}秒，{ev.Player.Nickname} 已自动变为观察者");
                }
                else
                {
                    ev.Player.Role.Set(RoleTypeId.Tutorial, Exiled.API.Enums.SpawnReason.Respawn, RoleSpawnFlags.All);
                    Log.Info($"[AutoTutorial] {ev.Player.Nickname} 已自动变为教程角色");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoTutorial] 处理玩家加入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取服务器当前准备倒计时秒数。
        /// 未在准备/无倒计时/读取失败时返回一个很大的值，表示"回合不会马上开始"，玩家将走教程角色分支。
        /// </summary>
        private float GetCountdownSeconds()
        {
            try
            {
                // 通过反射读取 GameCore.RoundStart 单例与 NetworkTimer(SyncVar float)。
                // 用反射避免对程序集/类型名的强依赖导致编译失败。
                var roundStartType = Type.GetType("GameCore.RoundStart, Assembly-CSharp-firstpass")
                                    ?? Type.GetType("GameCore.RoundStart, Assembly-CSharp");
                if (roundStartType == null) return float.MaxValue;

                var singletonProp = roundStartType.GetProperty("singleton");
                object singleton = singletonProp?.GetValue(null, null);
                if (singleton == null) return float.MaxValue;

                float timer = float.MaxValue;
                var timerProp = roundStartType.GetProperty("NetworkTimer");
                if (timerProp != null)
                {
                    var val = timerProp.GetValue(singleton, null);
                    if (val is float f) timer = f;
                }
                else
                {
                    var timerField = roundStartType.GetField("NetworkTimer");
                    var val = timerField?.GetValue(singleton);
                    if (val is float f2) timer = f2;
                }

                // timer <= 0 表示当前无进行中的准备倒计时 => 视为不触发观察者分支
                if (timer <= 0f) return float.MaxValue;
                return timer;
            }
            catch (Exception ex)
            {
                if (Config.Debug) Log.Debug($"[AutoTutorial] 读取准备倒计时失败: {ex.Message}");
                // 读取失败时保守处理：不确定倒计时，仍然变教程角色
                return float.MaxValue;
            }
        }
    }
}
