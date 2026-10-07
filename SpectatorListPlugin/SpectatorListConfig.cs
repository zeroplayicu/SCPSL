using System.ComponentModel;
using Exiled.API.Interfaces;
using HintServiceMeow.Core.Enum;

namespace SpectatorListPlugin
{
    public class SpectatorListConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("观战人数显示的屏幕Y坐标（HSM坐标，0=顶部，1080=底部）")]
        public int DisplayYCoordinate { get; set; } = 360;

        [Description("观战人数/名单显示的字体大小")]
        public int DisplayFontSize { get; set; } = 16;

        [Description("显示刷新间隔（毫秒），默认1000毫秒(1秒)")]
        public int RefreshIntervalMs { get; set; } = 1000;

        [Description("观战人数文本模板（{count}=正在观战我的玩家数，{total}=同值）")]
        public string CountTemplate { get; set; } = "👁 有 <color=yellow>{count}</color> 人在观战你";

        [Description("无人观战时显示的文字")]
        public string NoSpectatorText { get; set; } = "👁 当前没有人在观战你";

        [Description("详细名单标题")]
        public string DetailHeader { get; set; } = "—— 正在观战你的人 ——";

        [Description("详细名单每条名字模板（{name}=玩家名，{nickname}=角色显示名）")]
        public string DetailLineTemplate { get; set; } = "<color=#AAAAAA>•</color> {name}";

        [Description("观战列表显示位置：Center=屏幕中央，Left=左侧，Right=右侧")]
        public HintAlignment Alignment { get; set; } = HintAlignment.Right;
    }
}
