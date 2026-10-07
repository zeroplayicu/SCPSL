using System.ComponentModel;
using Exiled.API.Interfaces;

namespace ServerStatusPlugin
{
    public class ServerStatusConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("Web服务端口（网页访问端口）")]
        public ushort WebPort { get; set; } = 911;

        [Description("服务器显示名字（默认名，若下方 ServerNames 配置了当前端口则优先用该端口对应的名字）")]
        public string ServerName { get; set; } = "SCP:SL 服务器";

        [Description("按端口修改服务器名字，格式: 端口:名字，用英文逗号分隔多个。例: 7777:主服务器,7778:娱乐服")]
        public string ServerNames { get; set; } = "";

        [Description("需要聚合展示的远程服务器状态地址列表，英文逗号分隔。格式: http://IP:911 或 http://IP:端口（每台远程服务器都需部署本插件并开放911网页）。例: http://47.96.110.171:911,http://8.8.8.8:911")]
        public string ServerEndpoints { get; set; } = "";

        [Description("网页刷新间隔（秒），用于自动刷新运行状况/CPU/内存")]
        public int RefreshInterval { get; set; } = 5;

        [Description("页面标题")]
        public string PageTitle { get; set; } = "服务器状态监控";

        /// <summary>
        /// 按 endpoint 推断端口(911/9111/9112 等)→ 返回对应的 server_names 名字。
        /// 用于离线卡片显示游戏名(不再用 endpoint URL)。
        /// </summary>
        public string GetPortName(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(ServerNames))
                return null;
            try
            {
                var uri = new System.Uri(endpoint);
                ushort port = (ushort)uri.Port;
                foreach (var part in ServerNames.Split(','))
                {
                    var kv = part.Split(new[] { ':' }, 2);
                    if (kv.Length == 2 && ushort.TryParse(kv[0].Trim(), out ushort p) && p == port)
                        return kv[1].Trim();
                }
            }
            catch { }
            return null;
        }
    }
}
