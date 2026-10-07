using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ExperiencePlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class SettingsCommand : ICommand
    {
        public string Command => "settings";
        public string[] Aliases => new[] { "set", "设置", "sz" };
        public string Description => "打开玩家设置界面: .settings";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var plugin = ExperiencePlugin.Instance;
                var data = plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) { response = "无法读取玩家数据"; return false; }

                // 检查VIP是否过期
                if (data.VipLevel > 0 && data.VipExpiry != DateTime.MinValue && data.VipExpiry != DateTime.MaxValue)
                {
                    if (DateTime.Now > data.VipExpiry)
                    {
                        data.VipLevel = 0;
                        data.VipExpiry = DateTime.MinValue;
                        plugin.DataManager.SaveAllData();
                        plugin.EventHandler.RefreshSingleVipBadge(player, data);
                    }
                }

                // 构建VIP状态文字
                string vipStatus;
                string vipTimeLeft = "";
                if (data.VipLevel <= 0)
                {
                    vipStatus = "<color=#AAAAAA>无</color>";
                }
                else
                {
                    string typeName = data.VipLevel >= 2 ? "SVIP" : "VIP";
                    string typeColor = data.VipLevel >= 2 ? "#FF69B4" : "#FFD700";
                    if (data.VipExpiry == DateTime.MaxValue)
                        vipTimeLeft = "永久";
                    else
                    {
                        int daysLeft = (int)(data.VipExpiry - DateTime.Now).TotalDays;
                        int hoursLeft = (int)(data.VipExpiry - DateTime.Now).TotalHours % 24;
                        vipTimeLeft = daysLeft > 0 ? $"{daysLeft}天{hoursLeft}小时" : $"{hoursLeft}小时";
                    }
                    vipStatus = $"<color={typeColor}>{typeName}</color> (<color=#AAAAAA>{vipTimeLeft}</color>)";
                }

                // 头衔开关
                string titleStatus = data.ShowVipTitle ? "<color=#44FF88>● 开启</color>" : "<color=#FF4444>○ 关闭</color>";

                // 注意：HSM 不支持内联 <size> 标签（会被大写化后显示字面量），字号统一由 HsmHint.FontSize 控制
                string hintText =
                    "<color=#FFD700>⚙ 玩家设置</color>\n" +
                    "<color=#666666>────────────</color>\n" +
                    $"VIP/SVIP: {vipStatus}\n" +
                    $"头衔显示: {titleStatus}\n" +
                    "<color=#AAAAAA>.vipbadge 切换显示/隐藏</color>\n" +
                    "<color=#666666>────────────</color>\n" +
                    "<color=#FFD700>🎰 抽奖</color>\n" +
                    $"单抽 <color=#FFD700>{plugin.Config.LotterySingleCost}积分</color> / 五连抽 <color=#FFD700>{plugin.Config.Lottery5Cost}积分</color>\n" +
                    "<color=#AAAAAA>.lottery 1 单抽 / .lottery 5 五连</color>\n" +
                    "<color=#AAAAAA>.lottery confirm 确认抽取</color>\n" +
                    $"<color=#FFD700>当前积分: {data.Points:F1}</color>";

                try
                {
                    PlayerDisplay.Get(player).ShowHint(new HsmHint
                    {
                        Id = "player_settings",
                        Text = hintText,
                        FontSize = 14,
                        YCoordinate = 200,
                        Alignment = HintAlignment.Center
                    }, 10f);
                }
                catch { }
                // 备选：Broadcast 确保关键信息可见
                player.Broadcast(8, $"<color=#FFD700>⚙ 设置</color> | VIP: {(data.VipLevel>=2?"SVIP":data.VipLevel>=1?"VIP":"无")} | 头衔: {(data.ShowVipTitle?"开":"关")} | 积分: {data.Points:F1} | 抽奖: 单抽{plugin.Config.LotterySingleCost}分/五连{plugin.Config.Lottery5Cost}分");

                response = "已打开设置界面，请在游戏中查看";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"设置界面错误: {ex.Message}");
                response = "打开设置失败";
                return false;
            }
        }
    }
}
