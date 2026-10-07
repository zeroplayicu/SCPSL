using System.ComponentModel;
using Exiled.API.Interfaces;

namespace StandRegenPlugin
{
    public class StandRegenConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("Debug模式")]
        public bool Debug { get; set; } = false;

        [Description("站立不动多少秒后开始回血")]
        public float StandDelaySeconds { get; set; } = 5f;

        [Description("每秒回血量")]
        public float RegenPerSecond { get; set; } = 6f;

        [Description("SCP173每秒回血量")]
        public float Scp173RegenPerSecond { get; set; } = 8f;

        [Description("检测/回血刷新间隔（毫秒）")]
        public int RefreshIntervalMs { get; set; } = 1000;

        [Description("位置变化判定阈值（米），低于此距离视为站立不动")]
        public float StandStillThreshold { get; set; } = 0.1f;

        [Description("是否只对 SCP 生效（true=仅SCP，false=所有玩家）")]
        public bool OnlyScp { get; set; } = true;

        [Description("回血是否显示提示")]
        public bool ShowHint { get; set; } = true;
    }
}
