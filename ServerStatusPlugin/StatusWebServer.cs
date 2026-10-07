using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Exiled.API.Features;
using Exiled.API.Enums;

namespace ServerStatusPlugin
{
    public class StatusWebServer
    {
        private readonly ServerStatusConfig _config;
        private readonly ServerStatusPlugin _plugin;
        private HttpListener _listener;
        private Thread _serverThread;
        private volatile bool _running;

        // CPU 采样状态
        private readonly object _cpuLock = new object();
        private long _lastCpuTime;
        private DateTime _lastCpuStamp;
        private bool _cpuInitialized;

        public StatusWebServer(ServerStatusConfig config, ServerStatusPlugin plugin)
        {
            _config = config;
            _plugin = plugin;
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://+:" + _config.WebPort + "/");
                _listener.Start();
                _running = true;
                _serverThread = new Thread(ServerLoop) { IsBackground = true };
                _serverThread.Start();
                Log.Info($"[ServerStatus] 网页服务已启动: http://0.0.0.0:{_config.WebPort}/");
            }
            catch (Exception ex)
            {
                Log.Error("[ServerStatus] 启动Web服务失败: " + ex.Message);
                Log.Error("[ServerStatus] 请以管理员身份运行服务器，或使用: netsh http add urlacl url=http://+:" + _config.WebPort + "/ user=Everyone");
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
        }

        private void ServerLoop()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    if (_running) Log.Error("[ServerStatus] 服务器循环: " + ex.Message);
                }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var resp = ctx.Response;
                string path = req.Url.AbsolutePath.ToLower();

                // 简单路由：主页 / 和 /api/status 返回 JSON
                if (path == "/" || path == "/index" || path == "/index.html")
                {
                    SendHtml(resp, BuildIndexPage());
                }
                else if (path == "/api/status")
                {
                    SendJson(resp, BuildStatusJson());
                }
                else
                {
                    resp.StatusCode = 404;
                    SendHtml(resp, "<html><body><h2>404 页面不存在</h2></body></html>");
                }
            }
            catch (Exception ex)
            {
                Log.Error("[ServerStatus] 请求处理: " + ex.Message);
                try { ctx.Response.StatusCode = 500; SendHtml(ctx.Response, "<html><body><h2>服务器内部错误</h2></body></html>"); }
                catch { }
            }
        }

        // ========== 状态数据采集 ==========

        /// <summary>
        /// 服务器当前 CPU 占用率(百分比, 0~100)。
        /// 通过采样进程 CPU 时间差值计算，跨平台、无需管理员权限。
        /// </summary>
        private double GetCpuUsage()
        {
            try
            {
                using (var proc = Process.GetCurrentProcess())
                {
                    long cpuTime = proc.TotalProcessorTime.Ticks;
                    DateTime now = DateTime.UtcNow;
                    int processorCount = Environment.ProcessorCount;

                    lock (_cpuLock)
                    {
                        if (!_cpuInitialized)
                        {
                            _lastCpuTime = cpuTime;
                            _lastCpuStamp = now;
                            _cpuInitialized = true;
                            return 0;
                        }

                        double cpuDelta = cpuTime - _lastCpuTime;
                        double timeDelta = (now - _lastCpuStamp).TotalMilliseconds * TimeSpan.TicksPerMillisecond;

                        _lastCpuTime = cpuTime;
                        _lastCpuStamp = now;

                        if (timeDelta <= 0) return 0;

                        // 已用 CPU 时间增量 / 经过总时间 / 核心数 * 100
                        double percent = cpuDelta / timeDelta / processorCount * 100.0;
                        if (percent < 0) percent = 0;
                        if (percent > 100) percent = 100;
                        return Math.Round(percent, 1);
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 服务器内存占用信息。返回当前进程已用内存(MB)和系统总内存(MB)。
        /// usedMb 直接用 Process.WorkingSet64（真实物理内存，不强制最小值，避免出现"1MB"假象）
        /// totalMb 用 P/Invoke GlobalMemoryStatusEx 拿系统总内存；拿不到时 totalMb 保持 0（前端显示"未知"）。
        /// 注意：不要依赖 Microsoft.VisualBasic / System.Management 等 EXILED 环境可能缺失的程序集。
        /// </summary>
        private void GetMemory(out long usedMb, out long totalMb)
        {
            usedMb = 0;
            totalMb = 0;
            try
            {
                using (var proc = Process.GetCurrentProcess())
                {
                    // 进程工作集(物理内存) MB，直接使用真实值，不要用 Math.Max(1,..) 否则会把小内存撑成 1MB
                    long ws = proc.WorkingSet64 / 1024 / 1024;
                    if (ws < 0) ws = 0;
                    usedMb = ws;
                }
            }
            catch
            {
                usedMb = 0;
            }

            // 系统总物理内存：P/Invoke GlobalMemoryStatusEx（Windows 主路径）
            try
            {
                var mem = new MemoryStatusEx();
                if (NativeMethods.GlobalMemoryStatusEx(mem))
                {
                    long t = (long)(mem.ullTotalPhys / 1024 / 1024);
                    if (t > 0) totalMb = t;
                }
            }
            catch
            {
                // 忽略，下面用兜底
            }

            // 兜底：GC 报告的进程占用做参考（不是系统总量，但至少 usedMb 不会是 0）
            if (usedMb <= 0)
            {
                try
                {
                    long gcMb = GC.GetTotalMemory(false) / 1024 / 1024;
                    if (gcMb > 0) usedMb = gcMb;
                }
                catch { }
            }
        }

        /// <summary>
        /// 获取内存占用率百分比。totalMb 拿不到时返回 -1（前端按"未知"渲染）。
        /// </summary>
        private double GetMemoryUsage()
        {
            GetMemory(out long used, out long total);
            if (total <= 0) return -1;
            double p = (double)used / total * 100.0;
            if (p > 100) p = 100;
            if (p < 0) p = 0;
            return Math.Round(p, 1);
        }

        // ========== 页面/JSON 构建 ==========

        /// <summary>
        /// 聚合状态 JSON：本服务器 + 所有配置的远程服务器状态。
        /// 结构: { "current": {本服务器...}, "servers": [ {endpoint, ...远程}, ... ] }
        /// 远程服务器通过其各自的 /api/status 拉取（每台都需部署本插件）。
        /// </summary>
        private string BuildStatusJson()
        {
            double cpu = GetCpuUsage();
            GetMemory(out long usedMb, out long totalMb);
            double memPct = GetMemoryUsage();

            int onlinePlayers = SafePlayerCount();
            string status = "在线";

            // 本服务器
            var current = new StringBuilder();
            current.Append('{');
            current.Append("\"online\":true,");
            current.Append("\"serverName\":\"").Append(JsonEscape(_plugin.ResolvedServerName)).Append("\",");
            current.Append("\"port\":").Append(SafePort()).Append(',');
            current.Append("\"players\":").Append(onlinePlayers).Append(',');
            current.Append("\"maxPlayers\":").Append(SafeMaxPlayers()).Append(',');
            current.Append("\"cpu\":").Append(cpu.ToString("0.0")).Append(',');
            current.Append("\"memoryUsedMB\":").Append(usedMb).Append(',');
            current.Append("\"memoryTotalMB\":").Append(totalMb).Append(',');
            current.Append("\"memoryPercent\":").Append(memPct.ToString("0.0")).Append(',');
            current.Append("\"status\":\"").Append(status).Append("\",");
            current.Append("\"roundTime\":\"").Append(GetRoundTime()).Append("\",");
            current.Append("\"uptime\":\"").Append(GetUptime()).Append("\"");
            current.Append('}');

            // 远程服务器聚合
            var servers = new StringBuilder();
            servers.Append('[');
            bool first = true;
            foreach (var ep in GetRemoteEndpoints())
            {
                if (!first) servers.Append(',');
                first = false;
                servers.Append(FetchRemoteStatus(ep));
            }
            servers.Append(']');

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"current\":").Append(current).Append(',');
            sb.Append("\"servers\":").Append(servers);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>解析配置中的远程服务器地址列表（去重、去空白）</summary>
        private List<string> GetRemoteEndpoints()
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(_config.ServerEndpoints)) return list;
            foreach (var part in _config.ServerEndpoints.Split(','))
            {
                var ep = part.Trim().TrimEnd('/');
                if (ep.Length == 0) continue;
                if (!ep.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !ep.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    ep = "http://" + ep;
                if (!list.Contains(ep)) list.Add(ep);
            }
            return list;
        }

        /// <summary>
        /// 拉取远程服务器的 /api/status。
        /// 成功返回该服务器的状态 JSON（注入 endpoint + portName 字段）；失败返回离线占位 JSON（name 用配置映射的端口名）。
        /// </summary>
        private string FetchRemoteStatus(string endpoint)
        {
            // 优先用 server_names 配置的端口名；没配置就用 endpoint
            string displayName = _config.GetPortName(endpoint) ?? endpoint;
            var offline = "{\"endpoint\":\"" + JsonEscape(endpoint) + "\",\"online\":false,\"serverName\":\"" +
                          JsonEscape(displayName) + "\",\"port\":0,\"players\":0,\"maxPlayers\":0,\"cpu\":0,\"memoryUsedMB\":0,\"memoryTotalMB\":0,\"memoryPercent\":0,\"status\":\"离线\",\"roundTime\":\"—\",\"uptime\":\"—\"}";
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(endpoint + "/api/status");
                req.Method = "GET";
                req.Timeout = 3000;
                req.ReadWriteTimeout = 3000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json) || json[0] != '{')
                        return offline;
                    // 远程 JSON 与本地结构一致，注入 endpoint 字段；
                    // 若远程 serverName 是默认占位字符串(endpoint)或为空，用本地配置映射的端口名
                    string remoteName = ExtractJsonValue(json, "serverName");
                    if (string.IsNullOrEmpty(remoteName) || remoteName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || remoteName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        remoteName = displayName;
                    return "{\"endpoint\":\"" + JsonEscape(endpoint) + "\",\"serverName\":\"" + JsonEscape(remoteName) + "\"," + json.Substring(1);
                }
            }
            catch
            {
                return offline;
            }
        }

        /// <summary>从 JSON 字符串中提取顶级字段值(轻量解析,无需引库)</summary>
        private static string ExtractJsonValue(string json, string field)
        {
            try
            {
                string key = "\"" + field + "\":";
                int idx = json.IndexOf(key, StringComparison.Ordinal);
                if (idx < 0) return null;
                int start = idx + key.Length;
                // 跳过空白
                while (start < json.Length && (json[start] == ' ' || json[start] == '\t' || json[start] == '\n' || json[start] == '\r')) start++;
                if (start >= json.Length || json[start] != '"') return null;
                start++; // 跳过开始 "
                var sb = new StringBuilder();
                for (int i = start; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[i + 1]); i++; continue; }
                    if (c == '"') return sb.ToString();
                    sb.Append(c);
                }
            }
            catch { }
            return null;
        }

        private string BuildIndexPage()
        {
            string name = _plugin.ResolvedServerName;

            // 刷新间隔（秒），注入供 JS 使用
            int interval = Math.Max(1, _config.RefreshInterval);

            // 预生成远程服务器卡片占位（用配置的 server_names 映射名字，离线时也能正确显示游戏名）
            var remoteCards = new StringBuilder();
            foreach (var ep in GetRemoteEndpoints())
            {
                var portName = _config.GetPortName(ep) ?? "远程服务器";
                remoteCards.Append("<div class='card' data-ep='").Append(HtmlEscape(ep)).Append("'>")
                          .Append("<h2>").Append(HtmlEscape(portName)).Append("</h2>")
                          .Append("<div class='sub'>加载中...</div></div>");
            }

            return "<!DOCTYPE html>" +
"<html lang='zh-CN'>" +
"<head>" +
"<meta charset='utf-8'>" +
"<meta name='viewport' content='width=device-width, initial-scale=1'>" +
"<title>" + HtmlEscape(_config.PageTitle) + "</title>" +
"<style>" +
"body{font-family:'Microsoft YaHei',Arial,sans-serif;background:linear-gradient(135deg,#0f2027,#203a43,#2c5364);color:#e6f1f8;margin:0;min-height:100vh;padding:40px 16px;box-sizing:border-box}" +
".page-title{font-size:26px;margin:0 0 6px;text-align:center;color:#7fd1ff}" +
".page-sub{text-align:center;color:#9fb6c8;font-size:14px;margin-bottom:28px}" +
".cards{display:flex;flex-wrap:wrap;gap:24px;justify-content:center;max-width:1300px;margin:0 auto}" +
".card{background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.15);border-radius:16px;padding:24px 28px;width:360px;box-shadow:0 8px 32px rgba(0,0,0,.4)}" +
".card h2{font-size:20px;margin:0 0 4px;text-align:center;color:#7fd1ff;word-break:break-all}" +
".sub{text-align:center;color:#9fb6c8;font-size:14px;margin-bottom:16px}" +
".row{display:flex;justify-content:space-between;padding:10px 0;border-bottom:1px dashed rgba(255,255,255,.12);font-size:15px}" +
".row:last-child{border-bottom:none}" +
".lbl{color:#a9c2d4}.val{font-weight:bold}" +
".online{color:#54ff9f}.offline{color:#ff6b6b}" +
".bar{height:10px;border-radius:6px;background:rgba(255,255,255,.15);overflow:hidden;margin-top:6px}" +
".bar>div{height:100%;border-radius:6px;transition:width .5s}" +
".cpu .bar>div{background:linear-gradient(90deg,#4ecdc4,#44ea76)}" +
".mem .bar>div{background:linear-gradient(90deg,#f7b733,#fc4a1a)}" +
".status-dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:6px;animation:pulse 1.2s infinite}" +
".status-dot.online{background:#54ff9f}.status-dot.offline{background:#ff6b6b}" +
".status-dot.loading{background:#9fb6c8}" +
"@keyframes pulse{0%,100%{opacity:1}50%{opacity:.3}}" +
".foot{text-align:center;color:#6b8296;font-size:12px;margin-top:28px}" +
".tag{display:inline-block;font-size:12px;padding:2px 10px;border-radius:10px;margin-bottom:10px;background:rgba(127,209,255,.15);color:#7fd1ff}" +
"</style>" +
"</head>" +
"<body>" +
"<div class='page-title'>服务器状态</div>" +
"<div class='page-sub'>本服务器 · " + (GetRemoteEndpoints().Count + 1) + " 台服务器集群 · 数据每 " + interval + " 秒自动刷新</div>" +
"<div class='cards' id='cards'>" +
"<div class='card' id='card-local'>" +
"<div class='tag'>本服务器</div>" +
"<h2 id='c-name'>" + HtmlEscape(name) + "</h2>" +
"<div class='sub'><span class='status-dot loading' id='c-dot'></span><span id='c-status'>加载中...</span></div>" +
"<div class='row'><span class='lbl'>在线玩家</span><span class='val' id='c-players'>—</span></div>" +
"<div class='row cpu'><span class='lbl'>CPU 占用</span><span class='val' id='c-cpu'>—</span></div>" +
"<div class='row cpu'><div style='width:100%'><div class='bar'><div id='c-cpubar' style='width:0%'></div></div></div></div>" +
"<div class='row mem'><span class='lbl'>内存占用</span><span class='val' id='c-mem'>—</span></div>" +
"<div class='row mem'><div style='width:100%'><div class='bar'><div id='c-membar' style='width:0%'></div></div></div></div>" +
"<div class='row'><span class='lbl'>回合时长</span><span class='val' id='c-round'>—</span></div>" +
"<div class='row'><span class='lbl'>服务器运行时间</span><span class='val' id='c-uptime'>—</span></div>" +
"</div>" +
remoteCards.ToString() +
"</div>" +
"<div class='foot'>ServerStatusPlugin · 每台服务器需部署本插件并开放911端口</div>" +
"<script>" +
"function fill(card,s){" +
"var dot=card.querySelector('.status-dot');" +
// 修复 P0：JS 可选链不能作赋值目标（a?.b=c 是 SyntaxError，导致整个脚本解析失败、状态页永久"加载中"）。
// 状态文本已由下方 .sub 行覆盖，这里只更新圆点样式。
"if(s.online){dot.className='status-dot online'}" +
"else{dot.className='status-dot offline';}" +
"card.querySelector('.sub span:last-child').textContent=s.online?(s.status||'在线'):'离线';" +
"card.querySelector('.sub').innerHTML='<span class=\"status-dot '+(s.online?'online':'offline')+'\"></span>'+(s.online?(s.status||'在线'):'离线')+(s.port?' · 端口 '+s.port:'');" +
"var q=card.querySelectorAll('.row .val');" +
"if(s.online){" +
"q[0].textContent=s.players+' / '+s.maxPlayers;" +
"q[1].textContent=s.cpu+'%';" +
"card.querySelector('.cpu .bar>div').style.width=Math.min(100,s.cpu)+'%';" +
"q[2].textContent=(s.memoryPercent<0?'未知 ('+s.memoryUsedMB+'MB)':(s.memoryPercent+'% ('+s.memoryUsedMB+'MB)'));" +
"card.querySelector('.mem .bar>div').style.width=(s.memoryPercent<0?'0':Math.min(100,s.memoryPercent))+'%';" +
"q[3].textContent=s.roundTime;" +
"q[4].textContent=s.uptime;" +
"}else{" +
"q[0].textContent='—';q[1].textContent='—';q[2].textContent='—';q[3].textContent='—';q[4].textContent='—';" +
"}" +
"}" +
"function render(d){" +
"fill(document.getElementById('card-local'),d.current);" +
"var cards=document.querySelectorAll('.card[data-ep]');" +
"for(var i=0;i<cards.length;i++){var ep=cards[i].getAttribute('data-ep');var s=null;for(var j=0;j<d.servers.length;j++){if(d.servers[j].endpoint===ep){s=d.servers[j];break;}}" +
"if(s){cards[i].querySelector('h2').textContent=s.serverName||ep;" +
"cards[i].innerHTML=cardHtml(s);}" +
"}" +
"}" +
"function cardHtml(s){" +
"var dot=s.online?'online':'offline';var txt=s.online?(s.status||'在线'):'离线';" +
"var h='<div class=\"tag\">'+(s.online?'在线':'离线')+'</div>';" +
"h+='<h2>'+esc(s.serverName||'未知服务器')+'</h2>';" +
"h+='<div class=\"sub\"><span class=\"status-dot '+dot+'\"></span>'+txt+(s.port?' · 端口 '+s.port:'')+'</div>';" +
"h+='<div class=\"row\"><span class=\"lbl\">在线玩家</span><span class=\"val\">'+(s.online?(s.players+' / '+s.maxPlayers):'—')+'</span></div>';" +
"h+='<div class=\"row cpu\"><span class=\"lbl\">CPU 占用</span><span class=\"val\">'+(s.online?(s.cpu+'%'):'—')+'</span></div>';" +
"h+='<div class=\"row cpu\"><div style=\"width:100%\"><div class=\"bar\"><div style=\"width:'+(s.online?Math.min(100,s.cpu):0)+'%\"></div></div></div></div>';" +
"h+='<div class=\"row mem\"><span class=\"lbl\">内存占用</span><span class=\"val\">'+(s.online?(s.memoryPercent<0?('未知 ('+s.memoryUsedMB+'MB)'):(s.memoryPercent+'% ('+s.memoryUsedMB+'MB)')):'—')+'</span></div>';" +
"h+='<div class=\"row mem\"><div style=\"width:100%\"><div class=\"bar\"><div style=\"width:'+(s.online?(s.memoryPercent<0?0:Math.min(100,s.memoryPercent)):0)+'%\"></div></div></div></div>';" +
"h+='<div class=\"row\"><span class=\"lbl\">回合时长</span><span class=\"val\">'+(s.online?(s.roundTime||'—'):'—')+'</span></div>';" +
"h+='<div class=\"row\"><span class=\"lbl\">服务器运行时间</span><span class=\"val\">'+(s.online?(s.uptime||'—'):'—')+'</span></div>';" +
"return h;}" +
"function esc(s){return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\"/g,'&quot;');}" +
"function refresh(){" +
"fetch('/api/status').then(function(r){return r.json()}).then(function(d){render(d)}).catch(function(){});" +
"}" +
"refresh();setInterval(refresh," + (interval * 1000) + ");" +
"</script>" +
"</body></html>";
        }

        // ========== 工具方法 ==========

        private static int SafePlayerCount()
        {
            try
            {
                int count = 0;
                foreach (var p in Player.List)
                {
                    if (p != null && !p.IsNPC) count++;
                }
                return count;
            }
            catch { return 0; }
        }

        private static int SafeMaxPlayers()
        {
            try { return Server.MaxPlayerCount; }
            catch { return 0; }
        }

        private static ushort SafePort()
        {
            try { return Server.Port; }
            catch { return 0; }
        }

        private static string GetRoundTime()
        {
            try
            {
                if (!Round.IsStarted) return "未开始";
                var t = Round.ElapsedTime;
                return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
            }
            catch { return "—"; }
        }

        private static string GetUptime()
        {
            try
            {
                using (var proc = Process.GetCurrentProcess())
                {
                    var up = DateTime.UtcNow - proc.StartTime.ToUniversalTime();
                    return $"{(int)up.TotalHours}小时 {up.Minutes}分";
                }
            }
            catch { return "—"; }
        }

        private static void SendHtml(HttpListenerResponse resp, string html)
        {
            byte[] buf = Encoding.UTF8.GetBytes(html);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static void SendJson(HttpListenerResponse resp, string json)
        {
            byte[] buf = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static string HtmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        // ========== P/Invoke：系统物理内存 ==========
    }

    /// <summary>Windows 系统物理内存状态结构</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);
    }
}
