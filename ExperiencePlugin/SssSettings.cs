using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using PlayerRoles;

namespace ExperiencePlugin
{
    /// <summary>
    /// 在游戏内 Settings → Server-specific 中嵌入抽奖、头衔显示、聊天字体设置
    /// </summary>
    public class SssSettings
    {
        // ID（必须全局唯一，避免与其他插件冲突）
        private const int IdLotteryHeader   = 21001;
        private const int IdLotterySingle   = 21002;
        private const int IdLotteryFive     = 21003;
        private const int IdVipHeader       = 21010;
        private const int IdShowVipTitle    = 21011;
        private const int IdChatHeader      = 21020;
        private const int IdFontSizeSmall   = 21021;
        private const int IdFontSizeMedium  = 21022;
        private const int IdFontSizeLarge   = 21023;

        private readonly ExperiencePlugin _plugin;
        private List<SettingBase> _settings;

        public SssSettings(ExperiencePlugin plugin)
        {
            _plugin = plugin;
            Build();
        }

        private void Build()
        {
            _settings = new List<SettingBase>();

            // ==================== 1. 抽奖 ====================
            var hLottery = new HeaderSetting(IdLotteryHeader, "🎰 积分抽奖", padding: true);

            var btnSingle = new ButtonSetting(
                IdLotterySingle, $"单抽 ({_plugin.Config.LotterySingleCost} 积分)", "抽一次",
                holdTime: 0.5f,
                hintDescription: $"消耗 {_plugin.Config.LotterySingleCost} 积分进行一次抽奖",
                header: hLottery,
                onChanged: OnSingleLottery);

            var btnFive = new ButtonSetting(
                IdLotteryFive, $"十连抽 ({_plugin.Config.Lottery5Cost} 积分)", "抽十次",
                holdTime: 0.5f,
                hintDescription: $"消耗 {_plugin.Config.Lottery5Cost} 积分一次抽取五次（更划算）",
                header: hLottery,
                onChanged: OnFiveLottery);

            // 注意：Header 已通过选项的 header: 参数关联，不能再 _settings.Add(header)
            // 否则游戏会把该分组标题渲染两次（一次作为分组、一次作为独立条目）
            _settings.Add(btnSingle);
            _settings.Add(btnFive);

            // ==================== 2. 头衔显示 ====================
            var hVip = new HeaderSetting(IdVipHeader, "👑 头衔设置", padding: true);

            var showTitle = new TwoButtonsSetting(
                IdShowVipTitle, "VIP/SVIP 头衔", "隐藏", "显示",
                defaultIsSecond: true,
                hintDescription: "在名字前显示 VIP/SVIP 头衔（前缀）",
                collectionId: 255, isServerOnly: false,
                header: hVip,
                onChanged: OnShowVipTitle);

            // Header 仅通过选项关联（避免重复渲染）
            _settings.Add(showTitle);

            // ==================== 3. 聊天字体颜色 ====================
            var hChat = new HeaderSetting(IdChatHeader, "💬 聊天字体颜色", padding: true);

            var colorWhite = new ButtonSetting(
                IdFontSizeSmall, "白色 (#FFFFFF)", "选择",
                holdTime: 0.5f,
                hintDescription: "聊天字体设为白色（默认）",
                header: hChat,
                onChanged: OnColorWhite);

            var colorYellow = new ButtonSetting(
                IdFontSizeMedium, "黄色 (#FFD700)", "选择",
                holdTime: 0.5f,
                hintDescription: "聊天字体设为金色",
                header: hChat,
                onChanged: OnColorYellow);

            var colorGreen = new ButtonSetting(
                IdFontSizeLarge, "绿色 (#55DD55)", "选择",
                holdTime: 0.5f,
                hintDescription: "聊天字体设为绿色",
                header: hChat,
                onChanged: OnColorGreen);

            // Header 仅通过选项关联（避免重复渲染）
            _settings.Add(colorWhite);
            _settings.Add(colorYellow);
            _settings.Add(colorGreen);
        }

        // ==================== 发送 / 移除 ====================

        public void SendToPlayer(Player player)
        {
            try
            {
                // 同步玩家当前设置状态
                SyncCurrentState(player);
                SettingBase.Register(player, _settings);
                if (_plugin.Config.Debug)
                    Log.Debug($"[SSS] 已发送设置面板给 {player.Nickname}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[SSS] 发送给 {player.Nickname} 失败: {ex.Message}");
            }
        }

        public void RemoveFromPlayer(Player player)
        {
            try { SettingBase.Unregister(player, _settings); }
            catch { }
        }

        public void RemoveFromAll()
        {
            try { SettingBase.Unregister(p => true, _settings); }
            catch { }
        }

        /// <summary>同步玩家当前数据到设置项（如头衔开关状态）</summary>
        private void SyncCurrentState(Player player)
        {
            try
            {
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                // 同步头衔开关
                if (SettingBase.TryGetSetting<TwoButtonsSetting>(player, IdShowVipTitle, out var titleSetting))
                {
                    titleSetting.IsSecond = data.ShowVipTitle;
                    SettingBase.SendToPlayer(player, new SettingBase[] { titleSetting });
                }
            }
            catch { }
        }

        // ==================== 回调 ====================

        private void OnSingleLottery(Player player, SettingBase setting)
        {
            DoLottery(player, 1, _plugin.Config.LotterySingleCost);
        }

        private void OnFiveLottery(Player player, SettingBase setting)
        {
            DoLottery(player, 5, _plugin.Config.Lottery5Cost);
        }

        private void DoLottery(Player player, int count, float cost)
        {
            if (player == null) return;
            try
            {
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) { player.Broadcast(3, "<color=#FF4444>玩家数据未加载</color>"); return; }

                if (data.Points < cost)
                {
                    player.Broadcast(3, $"<color=#FF4444>积分不足!</color> 需要 {cost} 积分，你只有 {data.Points:F1}");
                    return;
                }

                // 扣分
                _plugin.DataManager.AddPoints(player.UserId, -cost);

                // 抽奖
                var results = _plugin.Lottery.Draw(count);
                if (results == null || results.Count == 0)
                {
                    player.Broadcast(3, "<color=#FF4444>抽奖异常，积分已退回</color>");
                    _plugin.DataManager.AddPoints(player.UserId, cost);
                    return;
                }

                // 发放奖品
                int vpCount = 0;
                var lines = new List<string>();
                lines.Add($"<size=22><color=#FFD700>★ 抽奖结果 ★</color></size>");

                foreach (var result in results)
                {
                    if (result?.Prize == null) continue;
                    var prize = result.Prize;

                    switch (prize.Type)
                    {
                        case "xp":
                            _plugin.DataManager.AddExperience(player, prize.Value);
                            lines.Add($"<size=14><color=#888888>[{result.Index}]</color> <color=lime>经验 +{prize.Value}</color></size>");
                            break;
                        case "points":
                            _plugin.DataManager.AddPoints(player.UserId, prize.Value);
                            lines.Add($"<size=14><color=#888888>[{result.Index}]</color> <color=yellow>积分 +{prize.Value}</color></size>");
                            break;
                        case "vip":
                        case "svip":
                            bool isSvip = prize.Type == "svip";
                            int newLevel = isSvip ? 2 : 1;
                            if (data.VipLevel < newLevel)
                                data.VipLevel = newLevel;

                            DateTime expiry;
                            if (data.VipExpiry == DateTime.MinValue || data.VipExpiry == DateTime.MaxValue)
                                expiry = DateTime.Now.AddDays(prize.Days);
                            else
                                expiry = data.VipExpiry.AddDays(prize.Days);
                            data.VipExpiry = expiry;

                            vpCount++;
                            string color = isSvip ? "#FF69B4" : "#FFD700";
                            lines.Add($"<size=14><color=#888888>[{result.Index}]</color> <color={color}>{prize.Name}</color></size>");
                            break;
                        default:
                            lines.Add($"<size=14><color=#888888>[{result.Index}]</color> {prize.Name}</size>");
                            break;
                    }
                }

                _plugin.DataManager.SaveAllData();

                if (vpCount > 0)
                {
                    // 使用 EventHandler 的统一 SetVipBadge（保留 admin 标签 + 等级前缀 + 【】中文括号）
                    _plugin.EventHandler?.SetVipBadge(player, data);
                    lines.Add($"<size=14><color=green>头衔已自动刷新</color></size>");
                }

                string mode = count == 1 ? "单抽" : "十连抽";
                player.Broadcast(5,
                    $"<color=#FFD700>【{mode}】</color> 消耗 {cost} 积分 | " +
                    $"获得 <color=#00FF00>{results.Count}</color> 个奖品 | " +
                    $"剩余积分 <color=yellow>{data.Points:F1}</color>");
            }
            catch (Exception ex)
            {
                Log.Error($"[SSS-抽奖] {ex.Message}");
                player.Broadcast(3, "<color=#FF4444>抽奖出错，请稍后重试</color>");
            }
        }

        private void OnShowVipTitle(Player player, SettingBase setting)
        {
            if (player == null) return;
            try
            {
                var btn = setting as TwoButtonsSetting;
                bool show = btn != null && btn.IsSecond;

                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                data.ShowVipTitle = show;
                _plugin.DataManager.SaveAllData();
                // 使用 EventHandler 的统一 SetVipBadge（保留 admin 标签 + 等级前缀）
                _plugin.EventHandler?.SetVipBadge(player, data);

                // player.Broadcast 不支持富文本(<color> 标签会显示字面量)，改用纯文本
                string bcText = $"头衔显示: 已 {(show ? "开启" : "关闭")}";
                player.Broadcast(3, bcText);
            }
            catch (Exception ex)
            {
                Log.Error($"[SSS-头衔] {ex.Message}");
            }
        }

        private void OnColorWhite(Player player, SettingBase setting)
        {
            SetChatColor(player, "#FFFFFF", "白色");
        }

        private void OnColorYellow(Player player, SettingBase setting)
        {
            SetChatColor(player, "#FFD700", "金色");
        }

        private void OnColorGreen(Player player, SettingBase setting)
        {
            SetChatColor(player, "#55DD55", "绿色");
        }

        private void SetChatColor(Player player, string hexColor, string colorName)
        {
            if (player == null) return;
            try
            {
                var data = _plugin.DataManager.GetPlayerData(player.UserId);
                if (data == null) return;

                data.ChatColor = hexColor;
                _plugin.DataManager.SaveAllData();

                player.Broadcast(3,
                    $"<color=#FFD700>聊天字体颜色</color> 已设为 <color={hexColor}>{colorName}</color>");
            }
            catch (Exception ex)
            {
                Log.Error($"[SSS-颜色] {ex.Message}");
            }
        }

        // 注：头衔刷新统一调用 _plugin.EventHandler.SetVipBadge()，保留 admin 标签 + 等级前缀
    }
}
