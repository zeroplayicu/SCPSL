using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;

namespace SpectatorListPlugin
{
    /// <summary>
    /// 在游戏内 Settings → Server-specific 中嵌入观战列表开关设置。
    /// 两个开关：观战人数显示 / 观战详细名单。
    /// ID 范围：22000-22009（全局唯一，避免与其他插件冲突，Experience 用 21000+，这里用 22000+）
    /// </summary>
    public class SpectatorSssSettings
    {
        private const int IdHeader     = 22000;
        private const int IdShowCount  = 22001;
        private const int IdShowDetail = 22002;

        private readonly SpectatorListPlugin _plugin;
        private readonly List<SettingBase> _settings;

        public SpectatorSssSettings(SpectatorListPlugin plugin)
        {
            _plugin = plugin;
            _settings = new List<SettingBase>();
            Build();
        }

        private void Build()
        {
            var header = new HeaderSetting(IdHeader, "👁 观战列表", padding: true);

            var showCount = new TwoButtonsSetting(
                IdShowCount, "观战列表", "关闭", "开启",
                defaultIsSecond: true,
                hintDescription: "在屏幕上显示当前有多少人在观战",
                collectionId: 255, isServerOnly: false,
                header: header,
                onChanged: OnShowCount);

            var showDetail = new TwoButtonsSetting(
                IdShowDetail, "观战列表详细", "关闭", "开启",
                defaultIsSecond: false,
                hintDescription: "在观战列表下方显示所有观战者的名字",
                collectionId: 255, isServerOnly: false,
                header: header,
                onChanged: OnShowDetail);

            // 注意：Header 已通过选项的 header: 参数关联，不能再 _settings.Add(header)
            // 否则游戏会把分组标题渲染两次
            _settings.Add(showCount);
            _settings.Add(showDetail);
        }

        public void SendToPlayer(Player player)
        {
            try
            {
                SyncCurrentState(player);
                SettingBase.Register(player, _settings);
                if (_plugin.Config.Debug)
                    Log.Debug($"[观战列表] 已发送设置面板给 {player.Nickname}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[观战列表] 发送设置失败: {ex.Message}");
            }
        }

        public void RemoveFromPlayer(Player player)
        {
            try { SettingBase.Unregister(player, _settings); } catch { }
        }

        public void RemoveFromAll()
        {
            try { SettingBase.Unregister(p => true, _settings); } catch { }
        }

        private void SyncCurrentState(Player player)
        {
            try
            {
                var data = _plugin.DataManager.Get(player.UserId);
                if (data == null) return;

                if (SettingBase.TryGetSetting<TwoButtonsSetting>(player, IdShowCount, out var countSetting))
                {
                    countSetting.IsSecond = data.ShowSpectatorCount;
                    SettingBase.SendToPlayer(player, new SettingBase[] { countSetting });
                }

                if (SettingBase.TryGetSetting<TwoButtonsSetting>(player, IdShowDetail, out var detailSetting))
                {
                    detailSetting.IsSecond = data.ShowSpectatorDetail;
                    SettingBase.SendToPlayer(player, new SettingBase[] { detailSetting });
                }
            }
            catch { }
        }

        private void OnShowCount(Player player, SettingBase setting)
        {
            if (player == null) return;
            try
            {
                var btn = setting as TwoButtonsSetting;
                bool show = btn != null && btn.IsSecond;
                _plugin.DataManager.GetOrCreate(player).ShowSpectatorCount = show;
                _plugin.DataManager.SaveAllData();
                player.Broadcast(3,
                    $"<color=#FFD700>观战列表</color> 已{(show ? "<color=lime>开启</color>" : "<color=#FF4444>关闭</color>")}");
            }
            catch (Exception ex)
            {
                Log.Error($"[观战列表] 观战人数开关出错: {ex.Message}");
            }
        }

        private void OnShowDetail(Player player, SettingBase setting)
        {
            if (player == null) return;
            try
            {
                var btn = setting as TwoButtonsSetting;
                bool show = btn != null && btn.IsSecond;
                _plugin.DataManager.GetOrCreate(player).ShowSpectatorDetail = show;
                _plugin.DataManager.SaveAllData();
                player.Broadcast(3,
                    $"<color=#FFD700>观战列表详细</color> 已{(show ? "<color=lime>开启</color>" : "<color=#FF4444>关闭</color>")}");
            }
            catch (Exception ex)
            {
                Log.Error($"[观战列表] 详细名单开关出错: {ex.Message}");
            }
        }
    }
}
