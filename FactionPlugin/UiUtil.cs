using Exiled.API.Features;

namespace FactionPlugin
{
    /// <summary>
    /// 统一插件提示位置为屏幕底部（HintServiceMeow PlayerDisplay）。
    /// 用户最终要求（2026-09-30 21:49）："全部改没" → 插件内的 ShowHint 调用全部不显示。
    /// </summary>
    public static class UiUtil
    {
        /// <summary>已禁用：用户要求所有插件提示全部不显示</summary>
        public static void ShowHintBottom(this Player player, string text, float duration = 2f)
        {
            // 全部关掉：插件内的提示一概不显示
        }
    }
}