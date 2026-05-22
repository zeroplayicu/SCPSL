using System;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.API.Features;

namespace MyFirstPlugin
{
    public class EventHandlers
    {
        private readonly Plugin plugin;

        public EventHandlers(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            Player player = ev.Player;

            if (plugin.Config.DebugMode)
            {
                Log.Debug($"玩家 {player.Nickname} (ID: {player.UserId}) 已加入服务器");
                Log.Debug($"  - 阵营: {player.Role.Side}");
                Log.Debug($"  - 房间: {player.CurrentRoom}");
            }

            player.Broadcast(
                plugin.Config.WelcomeDuration,
                $"<color=yellow><b>{plugin.Config.WelcomeMessage}</b></color>\n" +
                $"<color=gray>服务器地址: 您的服务器IP</color>",
                Broadcast.BroadcastFlags.Normal
            );

            Log.Info($"玩家 {player.Nickname} 已加入游戏");
        }

        public void OnRoundStarted()
        {
            Log.Info("========================================");
            Log.Info($"回合 #{Round.CurrentRound} 已开始！");
            Log.Info($"当前玩家数: {Player.Dictionary.Count}");
            Log.Info("========================================");

            if (plugin.Config.BroadcastOnRoundStart)
            {
                foreach (var player in Player.List)
                {
                    player.Broadcast(
                        plugin.Config.RoundStartDuration,
                        $"<color=green><b>{plugin.Config.RoundStartMessage}</b></color>",
                        Broadcast.BroadcastFlags.Normal
                    );
                }
            }
        }

        public void OnPlayerDied(DiedEventArgs ev)
        {
            Player player = ev.Target;

            string attackerInfo = ev.Attacker != null
                ? $" 被 {ev.Attacker.Nickname} 击杀"
                : "";

            Log.Info($"玩家 {player.Nickname} 死亡{attackerInfo}");

            if (plugin.Config.DebugMode && ev.Attacker != null)
            {
                Log.Debug($"  - 伤害类型: {ev.DamageHandler?.Type}");
                Log.Debug($"  - 伤害来源: {ev.Attacker.Role}");
            }
        }
    }
}
