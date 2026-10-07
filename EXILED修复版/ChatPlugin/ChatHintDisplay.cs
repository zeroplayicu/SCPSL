using System;
using Exiled.API.Features;
using HintServiceMeow.Core.Utilities;
using HintServiceMeow.Core.Enum;
using ChatHsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace ChatPlugin
{
    /// <summary>
    /// UI-02修复: 聊天堆叠显示统一走 HSM。
    /// 原实现每次发消息都 ClearBroadcasts() + Broadcast()，会清掉其他插件的广播
    /// （例如 CleanupPlugin 的清扫倒计时警告），且聊天堆叠会与其它广播互相排队、延迟显示。
    /// HSM 按 Id 覆盖自身上一条堆叠，与其它插件的提示层完全解耦。
    /// Y 坐标避让: chat_display=40(顶部) / ac_display=200 / anti_tk_alert=400 /
    ///             cleanup_warning=800 / settle_hint=930
    /// </summary>
    public static class ChatHintDisplay
    {
        public const string HintId = "chat_display";
        public const int HintY = 40;

        /// <summary>显示（或覆盖）该玩家的聊天堆叠层</summary>
        public static void Show(Player target, string text, string fontSize, ushort duration)
        {
            if (target == null || !target.IsConnected) return;
            try
            {
                int size;
                if (!int.TryParse(fontSize, out size) || size <= 0) size = 16;

                var hint = new ChatHsmHint
                {
                    Id = HintId,
                    Text = text,
                    FontSize = size,
                    YCoordinate = HintY,
                    Alignment = HintAlignment.Center
                };
                PlayerDisplay.Get(target).ShowHint(hint, duration);
            }
            catch (Exception ex) { Log.Error($"聊天显示出错: {ex.Message}"); }
        }

        /// <summary>清空该玩家的聊天堆叠层（回合切换 / 插件卸载时调用）</summary>
        public static void Clear(Player target)
        {
            if (target == null || !target.IsConnected) return;
            try
            {
                var hint = new ChatHsmHint
                {
                    Id = HintId,
                    Text = " ",
                    FontSize = 16,
                    YCoordinate = HintY,
                    Alignment = HintAlignment.Center
                };
                PlayerDisplay.Get(target).ShowHint(hint, 1f);
            }
            catch { }
        }
    }
}
