using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace ServerStatusPlugin
{
    public class ServerStatusPlugin : Plugin<ServerStatusConfig>
    {
        public static ServerStatusPlugin Instance { get; private set; }

        public override string Name => "ServerStatusPlugin";
        public override string Author => "Developer";
        public override string Prefix => "srvstatus";

        /// <summary>Web 服务</summary>
        public StatusWebServer WebSrv { get; private set; }

        /// <summary>当前服务器名字（根据实际端口从配置中解析）</summary>
        public string ResolvedServerName => GetServerName();

        /// <summary>端口 -> 服务器名字 映射（从配置 ServerNames 解析）</summary>
        private readonly Dictionary<ushort, string> _serverNameMap = new Dictionary<ushort, string>();

        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"  {Name} v{Version} 加载中...");

            try
            {
                ParseServerNames();

                // 启动 911 网页服务
                WebSrv = new StatusWebServer(Config, this);
                WebSrv.Start();

                Log.Info($"{Name} 加载完成 — 状态网页: http://0.0.0.0:{Config.WebPort}/ (服务器名: {ResolvedServerName})");
            }
            catch (Exception ex)
            {
                Log.Error($"{Name} 初始化失败: {ex.Message}\n{ex.StackTrace}");
            }

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            try
            {
                WebSrv?.Stop();
                WebSrv = null;
            }
            catch { }

            Instance = null;
            base.OnDisabled();
        }

        /// <summary>
        /// 解析配置中的端口->服务器名字映射，格式: 端口:名字,端口:名字
        /// </summary>
        private void ParseServerNames()
        {
            _serverNameMap.Clear();
            if (string.IsNullOrWhiteSpace(Config.ServerNames)) return;

            foreach (var part in Config.ServerNames.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = part.Trim();
                int idx = p.IndexOf(':');
                if (idx <= 0) continue;

                string portStr = p.Substring(0, idx).Trim();
                string name = p.Substring(idx + 1).Trim();
                if (ushort.TryParse(portStr, out ushort port) && !string.IsNullOrEmpty(name))
                    _serverNameMap[port] = name;
            }
        }

        /// <summary>
        /// 根据服务器当前端口获取显示名字。
        /// 优先用配置中该端口对应的名字，否则用 ServerName 默认值。
        /// </summary>
        public string GetServerName()
        {
            try
            {
                ushort currentPort = Server.Port;
                if (_serverNameMap.TryGetValue(currentPort, out string mappedName))
                    return mappedName;
            }
            catch { }

            return string.IsNullOrWhiteSpace(Config.ServerName) ? "SCP:SL 服务器" : Config.ServerName;
        }
    }
}
