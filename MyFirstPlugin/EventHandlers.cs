using System;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.API.Features;

namespace MyFirstPlugin
{
    /// <summary>
    /// 事件处理类 - 处理所有游戏事件
    /// </summary>
    public class EventHandlers
    {
        // 保存插件实例的引用，以便访问配置
        private readonly Plugin plugin;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public EventHandlers(Plugin plugin)
        {
            this.plugin = plugin;
        }

        /// <summary>
        /// 玩家通过验证时触发（玩家成功加入服务器）
        /// </summary>
        /// <param name="ev">验证事件参数</param>
        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            // 获取玩家信息
            Player player = ev.Player;
            
            // 记录日志（调试模式下）
            if (plugin.Config.DebugMode)
            {
                Log.Debug($"玩家 {player.Nickname} (ID: {player.UserId}) 已加入服务器");
                Log.Debug($"  - 阵营: {player.Role.Side}");
                Log.Debug($"  - 房间: {player.CurrentRoom}");
            }

            // 向玩家发送欢迎广播
            player.Broadcast(
                plugin.Config.WelcomeDuration,
                $"<color=yellow><b>{plugin.Config.WelcomeMessage}</b></color>\n" +
                $"<color=gray>服务器地址: 您的服务器IP</color>",
                BroadcastType.Broadcast
            );

            // 全局日志
            Log.Info($"玩家 {player.Nickname} 已加入游戏");
        }

        /// <summary>
        /// 回合开始时触发
        /// </summary>
        /// <param name="ev">回合开始事件参数</param>
        public void OnRoundStarted(RoundStartedEventArgs ev)
        {
            // 记录回合开始
            Log.Info("========================================");
            Log.Info($"回合 #{Round.CurrentRound} 已开始！");
            Log.Info($"当前玩家数: {Player.Dictionary.Count}");
            Log.Info("========================================");

            // 如果启用了回合开始广播
            if (plugin.Config.BroadcastOnRoundStart)
            {
                // 向所有玩家广播消息
                foreach (var player in Player.List)
                {
                    player.Broadcast(
                        plugin.Config.RoundStartDuration,
                        $"<color=green><b>{plugin.Config.RoundStartMessage}</b></color>",
                        BroadcastType.Broadcast
                    );
                }
            }
        }

        /// <summary>
        /// 玩家死亡时触发
        /// </summary>
        /// <param name="ev">死亡事件参数</param>
        public void OnPlayerDied(DiedEventArgs ev)
        {
            // 获取死亡玩家
            Player player = ev.Target;
            
            // 获取攻击者（如果有）
            string attackerInfo = ev.Attacker != null 
                ? $" 被 {ev.Attacker.Nickname} 击杀" 
                : "";

            // 记录日志
            Log.Info($"玩家 {player.Nickname} 死亡{attackerInfo}");

            // 调试模式下的额外信息
            if (plugin.Config.DebugMode && ev.Attacker != null)
            {
                Log.Debug($"  - 伤害类型: {ev.DamageHandler?.Type}");
                Log.Debug($"  - 伤害来源: {ev.Attacker.Role}");
            }
        }

        /// <summary>
        /// 玩家受伤时触发（可选实现）
        /// </summary>
        /// <param name="ev">受伤事件参数</param>
        public void OnPlayerHurt(HurtEventArgs ev)
        {
            if (plugin.Config.DebugMode)
            {
                Log.Debug($"玩家 {ev.Player.Nickname} 受到了 {ev.Amount} 点伤害");
            }
        }

        /// <summary>
        /// 玩家生成时触发（可选实现）
        /// </summary>
        /// <param name="ev">生成事件参数</param>
        public void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            if (plugin.Config.DebugMode)
            {
                Log.Debug($"玩家 {ev.Player.Nickname} 生成为 {ev.Player.Role}");
            }
        }
    }
}
