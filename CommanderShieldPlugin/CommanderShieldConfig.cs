namespace CommanderShieldPlugin
{
    public class CommanderShieldConfig
    {
        /// <summary>AHP最大值</summary>
        public int MaxShieldAHP { get; set; } = 50;

        /// <summary>HS最大值</summary>
        public int MaxShieldHS { get; set; } = 100;

        /// <summary>每秒回复量</summary>
        public int RegenPerTick { get; set; } = 1;

        /// <summary>HUD刷新间隔（秒）</summary>
        public float HudRefreshInterval { get; set; } = 1.0f;

        /// <summary>是否替换指挥官卡为O5权限卡</summary>
        public bool ReplaceCommanderCard { get; set; } = true;
    }
}
