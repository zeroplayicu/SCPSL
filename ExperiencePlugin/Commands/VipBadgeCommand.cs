using System;
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
    public class VipBadgeCommand : ICommand
    {
        public string Command => "vipbadge";
        public string[] Aliases => new[] { "title", "头衔" };
        public string Description => "切换VIP/SVIP头衔显示: .vipbadge";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null) { response = "无法获取玩家信息"; return false; }

                var plugin = ExperiencePlugin.Instance;
                var data = plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) { response = "无法读取玩家数据"; return false; }

                // 切换头衔显示状态
                data.ShowVipTitle = !data.ShowVipTitle;

                // 重新应用头衔
                plugin.EventHandler.RefreshSingleVipBadge(player, data);

                // 刷新显示
                plugin.EventHandler.RefreshAllDisplays();

                string status = data.ShowVipTitle ? "开启" : "关闭";
                string color = data.ShowVipTitle ? "#44FF88" : "#FF4444";

                // 注意：HSM 不支持内联 <size>（会显示字面量），字号由 HsmHint.FontSize 控制
                string hintText = "<color=#FFD700>⚙ 头衔设置</color>\n" +
                    $"VIP/SVIP头衔: <color={color}>{status}</color>";

                PlayerDisplay.Get(player).ShowHint(new HsmHint
                {
                    Id = "vip_toggle",
                    Text = hintText,
                    FontSize = 14,
                    YCoordinate = 350,
                    Alignment = HintAlignment.Center
                }, 4f);

                plugin.DataManager.SaveAllData();

                response = $"VIP头衔显示已{status}";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"VIP头衔切换错误: {ex.Message}");
                response = "切换失败";
                return false;
            }
        }
    }
}
