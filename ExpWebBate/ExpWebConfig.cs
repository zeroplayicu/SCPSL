using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ExpWebBate
{
    public class ExpWebConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("Web服务端口")]
        public ushort WebPort { get; set; } = 8080;

        [Description("数据库类型 (json)")]
        public string DatabaseType { get; set; } = "json";

        [Description("数据库连接地址（JSON模式下留空）")]
        public string DbConnectionString { get; set; } = "";

        [Description("数据库用户名（JSON模式下留空）")]
        public string DbUser { get; set; } = "";

        [Description("数据库密码（JSON模式下留空）")]
        public string DbPassword { get; set; } = "";

        [Description("管理员账号")]
        public string AdminUser { get; set; } = "admin";

        [Description("管理员密码（首次启动后强制修改）")]
        public string AdminPassword { get; set; } = "admin";

        [Description("是否允许游客查看排行榜")]
        public bool PublicLeaderboard { get; set; } = true;

        [Description("Debug模式")]
        public bool Debug { get; set; } = false;
    }
}
