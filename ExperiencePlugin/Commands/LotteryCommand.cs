using System;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class LotteryCommand : ICommand
    {
        public string Command => "lottery";
        public string[] Aliases => new[] { "draw", "抽奖" };
        public string Description => "积分抽奖: .lottery 1 (单抽) / .lottery 5 (五连抽) / .lottery confirm (确认)";

        // 待确认的抽奖请求
        private static readonly Dictionary<string, (int Count, DateTime Timeout)> _pending =
            new Dictionary<string, (int, DateTime)>();

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var plugin = ExperiencePlugin.Instance;
                var data = plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) { response = "无法读取玩家数据"; return false; }

                string userId = player.UserId;
                string subCmd = arguments.Count > 0 ? arguments.At(0).ToLower() : "";

                // ---- 确认抽奖 ----
                if (subCmd == "confirm" || subCmd == "yes" || subCmd == "y")
                {
                    if (!_pending.TryGetValue(userId, out var pending))
                    {
                        response = "没有待确认的抽奖请求";
                        return false;
                    }
                    if (DateTime.Now > pending.Timeout)
                    {
                        _pending.Remove(userId);
                        response = "抽奖确认已超时，请重新输入 .lottery 1 或 .lottery 5";
                        return false;
                    }

                    _pending.Remove(userId);
                    return ExecuteDraw(player, data, pending.Count, out response);
                }

                // ---- 取消抽奖 ----
                if (subCmd == "cancel" || subCmd == "no" || subCmd == "n")
                {
                    if (_pending.Remove(userId))
                    {
                        response = "已取消抽奖";
                        return true;
                    }
                    response = "没有待取消的抽奖请求";
                    return false;
                }

                // ---- 单抽 ----
                if (subCmd == "1")
                {
                    float cost = plugin.Config.LotterySingleCost;
                    if (data.Points < cost)
                    {
                        response = $"积分不足！需要 {cost} 积分，当前 {data.Points:F1} 积分";
                        return false;
                    }

                    int timeout = plugin.Config.LotteryConfirmTimeout;
                    _pending[userId] = (1, DateTime.Now.AddSeconds(timeout));
                    ShowConfirmHint(player, 1, cost, timeout);
                    response = $"消耗 {cost} 积分单抽，请在 {timeout} 秒内输入 .lottery confirm 确认";
                    return true;
                }

                // ---- 五连抽 ----
                if (subCmd == "5" || subCmd == "五连")
                {
                    float cost = plugin.Config.Lottery5Cost;
                    if (data.Points < cost)
                    {
                        response = $"积分不足！需要 {cost} 积分，当前 {data.Points:F1} 积分";
                        return false;
                    }

                    int timeout = plugin.Config.LotteryConfirmTimeout;
                    _pending[userId] = (5, DateTime.Now.AddSeconds(timeout));
                    ShowConfirmHint(player, 5, cost, timeout);
                    response = $"消耗 {cost} 积分五连抽，请在 {timeout} 秒内输入 .lottery confirm 确认";
                    return true;
                }

                // ---- 显示帮助 ----
                ShowLotteryHelp(player, data);
                response = "请在游戏内查看抽奖说明";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"抽奖命令错误: {ex.Message}");
                response = "抽奖失败";
                return false;
            }
        }

        /// <summary>执行实际抽奖</summary>
        private static bool ExecuteDraw(Player player, PlayerData data, int count, out string response)
        {
            try
            {
                var plugin = ExperiencePlugin.Instance;
                float cost = count == 1 ? plugin.Config.LotterySingleCost : plugin.Config.Lottery5Cost;

                // 再次检查积分（可能已被其他操作消耗）
                if (data.Points < cost)
                {
                    response = $"积分不足！需要 {cost} 积分，当前 {data.Points:F1} 积分";
                    return false;
                }

                // 扣除积分
                data.Points -= cost;

                // 执行抽奖
                var results = plugin.Lottery.Draw(count);
                // 去掉内联 <size> 标签(在HSM中会被错误处理导致字面量显示)，字号由 HsmHint.FontSize 控制；<color> 标签可正常解析
                var lines = new List<string>
                {
                    "<color=#FFD700>🎰 抽奖结果</color>",
                    "<color=#888888>──────────────</color>"
                };

                foreach (var result in results)
                {
                    string color = GetPrizeColor(result.Prize.Type);
                    lines.Add($"<color={color}>【{result.Index}/{count}】{result.Prize.Name}</color>");
                    ApplyPrize(player, data, result.Prize);
                }

                lines.Add("<color=#888888>──────────────</color>");
                lines.Add($"<color=#AAAAAA>剩余积分: {data.Points:F1}</color>");

            string hintText = string.Join("\n", lines);
            try
            {
                PlayerDisplay.Get(player).ShowHint(new HsmHint
                {
                    Id = "lottery_result",
                    Text = hintText,
                    FontSize = 18,
                    YCoordinate = 450,
                    Alignment = HintAlignment.Center
                }, 15f);
            }
            catch { }
            // 备选：Broadcast 确保玩家一定能看到
            string broadcastMsg = results.Count > 0
                ? $"<color=#FFD700>抽奖成功!</color> 获得 {results.Count} 个奖品 | 剩余积分: {data.Points:F1}"
                : $"<color=#FF4444>抽奖完成</color> 未获得奖品 | 剩余积分: {data.Points:F1}";
            player.Broadcast(5, broadcastMsg, Broadcast.BroadcastFlags.Normal);

                plugin.DataManager.SaveAllData();

                // 刷新显示（更新状态栏积分）
                plugin.EventHandler.RefreshAllDisplays();

                response = $"抽奖成功，共获得 {results.Count} 个奖品";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"执行抽奖错误: {ex.Message}");
                response = "抽奖执行失败";
                return false;
            }
        }

        /// <summary>应用奖品</summary>
        private static void ApplyPrize(Player player, PlayerData data, LotteryManager.Prize prize)
        {
            switch (prize.Type)
            {
                case "xp":
                    bool leveledUp = data.AddExperience(prize.Value, ExperiencePlugin.Instance.Config.BaseExpPerLevel);
                    if (leveledUp)
                        Log.Info($"[抽奖] {player.Nickname} 抽中 {prize.Name}，升级至 {data.Level}");
                    break;
                case "points":
                    data.Points += prize.Value;
                    Log.Info($"[抽奖] {player.Nickname} 抽中 {prize.Name}");
                    break;
                case "vip":
                    // VIP: 如果已有更高级别则不覆盖
                    if (data.VipLevel < 1)
                    {
                        data.VipLevel = 1;
                        data.VipExpiry = prize.Days <= 0 ? DateTime.MaxValue : DateTime.Now.AddDays(prize.Days);
                    }
                    else if (data.VipExpiry != DateTime.MaxValue)
                    {
                        // 已有VIP: 延长天数
                        data.VipExpiry = data.VipExpiry.AddDays(prize.Days);
                    }
                    ExperiencePlugin.Instance.EventHandler.RefreshSingleVipBadge(player, data);
                    Log.Info($"[抽奖] {player.Nickname} 抽中{prize.Name}");
                    break;
                case "svip":
                    if (data.VipLevel < 2)
                    {
                        data.VipLevel = 2;
                        data.VipExpiry = prize.Days <= 0 ? DateTime.MaxValue : DateTime.Now.AddDays(prize.Days);
                    }
                    else if (data.VipExpiry != DateTime.MaxValue)
                    {
                        data.VipExpiry = data.VipExpiry.AddDays(prize.Days);
                    }
                    ExperiencePlugin.Instance.EventHandler.RefreshSingleVipBadge(player, data);
                    Log.Info($"[抽奖] {player.Nickname} 抽中{prize.Name}");
                    break;
            }
        }

        /// <summary>根据奖品类型返回颜色</summary>
        private static string GetPrizeColor(string type)
        {
            return type switch
            {
                "xp" => "#44FF88",
                "points" => "#FFD700",
                "vip" => "#FF69B4",
                "svip" => "#FF1493",
                _ => "#FFFFFF"
            };
        }

        /// <summary>显示确认提示</summary>
        private static void ShowConfirmHint(Player player, int count, float cost, int timeout)
        {
            string hintText = $"<color=#FFD700>🎰 确认抽奖</color>\n" +
                $"消耗 <color=#FFD700>{cost} 积分</color> {(count == 1 ? "单抽" : "五连抽")}\n" +
                $"\n\n" +
                $"<color=#44FF88>.lottery confirm</color> - 确认\n" +
                $"<color=#FF4444>.lottery cancel</color> - 取消\n" +
                $"<color=#AAAAAA>倒计时 {timeout} 秒</color>";

            try
            {
                PlayerDisplay.Get(player).ShowHint(new HsmHint
                {
                    Id = "lottery_confirm",
                    Text = hintText,
                    FontSize = 14,
                    YCoordinate = 480,
                    Alignment = HintAlignment.Center
                }, timeout);
            }
            catch { }
            // 备选：Broadcast
            player.Broadcast((ushort)Math.Min(timeout, 30), $"<color=#FFD700>确认抽奖</color> 消耗{cost}积分({(count==1?"单抽":"五连抽")}) | .lottery confirm 确认 | .lottery cancel 取消", Broadcast.BroadcastFlags.Normal);
        }

        /// <summary>显示抽奖帮助（当玩家输入 .lottery 无参数时）</summary>
        private static void ShowLotteryHelp(Player player, PlayerData data)
        {
            var plugin = ExperiencePlugin.Instance;
            float singleCost = plugin.Config.LotterySingleCost;
            float fiveCost = plugin.Config.Lottery5Cost;

            string helpText = $"<color=#FFD700>🎰 积分抽奖</color>\n" +
                $"<color=#AAAAAA>当前积分: {data.Points:F1}</color>\n" +
                $"\n" +
                $".lottery 1 - <color=#FFD700>单抽 {singleCost} 积分</color>\n" +
                $".lottery 5 - <color=#FFD700>五连抽 {fiveCost} 积分</color>\n" +
                $"\n" +
                $"<color=#AAAAAA>输入后需在倒计时内确认</color>\n" +
                $"<color=#AAAAAA>.lottery confirm 确认抽取</color>";

            try
            {
                PlayerDisplay.Get(player).ShowHint(new HsmHint
                {
                    Id = "lottery_help",
                    Text = helpText,
                    FontSize = 14,
                    YCoordinate = 480,
                    Alignment = HintAlignment.Center
                }, 8f);
            }
            catch { }
            // 备选：Broadcast
            player.Broadcast(6, $"<color=#FFD700>积分抽奖</color> 积分:{data.Points:F1} | 单抽{singleCost}分 / 五连{fiveCost}分 | .lottery 1 或 .lottery 5", Broadcast.BroadcastFlags.Normal);
        }
    }
}
