using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Exiled.API.Features;


namespace ExpWebBate
{
    public class WebServer
    {
        private readonly ExpWebConfig _config;
        private readonly DataStore _store;
        private readonly AuthManager _auth;
        private readonly PlayerDataManager _data;
        private readonly ForumManager _forum;
        private HttpListener _listener;
        private Thread _serverThread;
        private volatile bool _running;

        public WebServer(ExpWebConfig config, DataStore store, AuthManager auth, PlayerDataManager data, ForumManager forum)
        {
            _config = config;
            _store = store;
            _auth = auth;
            _data = data;
            _forum = forum;
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
                Log.Info("[ExpWebBate] Web服务已启动: http://0.0.0.0:" + _config.WebPort + "/");
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate] 启动Web服务失败: " + ex.Message);
                Log.Error("[ExpWebBate] 请以管理员身份运行服务器，或使用 netsh http add urlacl url=http://+:" + _config.WebPort + "/ user=Everyone");
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
                    if (_running) Log.Error("[ExpWebBate] 服务器循环: " + ex.Message);
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
                string method = req.HttpMethod.ToUpper();

                // CORS 头
                resp.Headers.Add("Access-Control-Allow-Origin", "*");
                resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (method == "OPTIONS")
                {
                    resp.StatusCode = 204;
                    resp.Close();
                    return;
                }

                // ===== 路由 =====
                if (path == "/" || path == "/login" || path == "/login.html")
                    SendHtml(resp, WebPages.LoginPage());
                else if (path == "/dashboard" || path == "/dashboard.html")
                    SendHtml(resp, WebPages.DashboardPage(GetSessionUser(req)));
                else if (path == "/lottery" || path == "/lottery.html")
                    SendHtml(resp, WebPages.LotteryPage(GetSessionUser(req)));
                else if (path == "/redeem" || path == "/redeem.html")
                    SendHtml(resp, WebPages.RedeemPage(GetSessionUser(req)));
                else if (path == "/admin" || path == "/admin.html")
                {
                    string token = GetAuthToken(req);
                    if (_auth.IsAdminSession(token) && _auth.ValidateSession(token))
                    {
                        bool needPwd = _auth.NeedPasswordChange();
                        SendHtml(resp, WebPages.AdminPage(token, needPwd));
                    }
                    else
                        Redirect(resp, "/login");
                }
                else if (path == "/api/login" && method == "POST")
                    HandleLogin(ctx);
                else if (path == "/api/logout")
                    HandleLogout(ctx);
                else if (path == "/api/me")
                    HandleMe(ctx);
                else if (path == "/api/players")
                    HandlePlayers(ctx);
                else if (path == "/api/lottery")
                    HandleLottery(ctx);
                else if (path == "/api/leaderboard")
                    HandleLeaderboard(ctx);
                else if (path == "/api/admin/changepwd" && method == "POST")
                    HandleChangePassword(ctx);
                else if (path == "/api/redeem" && method == "POST")
                    HandleRedeem(ctx);
                else if (path == "/ban" || path == "/ban.html")
                {
                    string token = GetAuthToken(req);
                    if (_auth.IsAdminSession(token) && _auth.ValidateSession(token))
                        SendHtml(resp, WebPages.BanPage(token));
                    else
                        Redirect(resp, "/login");
                }
                else if (path == "/api/ban" && method == "POST")
                    HandleBan(ctx);
                else if (path == "/api/ban/unban" && method == "POST")
                    HandleUnban(ctx);
                else if (path == "/forum" || path == "/forum.html")
                    SendHtml(resp, WebPages.ForumPage(GetSessionUser(req), GetAuthToken(req)));
                else if (path == "/forum/new" || path == "/forum/new.html")
                    SendHtml(resp, WebPages.ForumNewPage(GetSessionUser(req)));
                else if (path == "/forum/post" || path.StartsWith("/forum/post?"))
                    HandleForumPostView(ctx);
                else if (path == "/report" || path == "/report.html")
                    SendHtml(resp, WebPages.ReportPage(GetSessionUser(req), GetAuthToken(req)));
                else if (path == "/admin/reports" || path == "/admin/reports.html")
                {
                    string token = GetAuthToken(req);
                    if (_auth.IsAdminSession(token) && _auth.ValidateSession(token))
                        SendHtml(resp, WebPages.AdminReportsPage(token));
                    else
                        Redirect(resp, "/login");
                }
                else if (path == "/admin/forum" || path == "/admin/forum.html")
                {
                    string token = GetAuthToken(req);
                    if (_auth.IsAdminSession(token) && _auth.ValidateSession(token))
                        SendHtml(resp, WebPages.AdminForumPage(token));
                    else
                        Redirect(resp, "/login");
                }
                // ===== API 路由 =====
                else if (path == "/api/forum/posts")
                    HandleForumPosts(ctx);
                else if (path == "/api/forum/post" && method == "POST")
                    HandleForumCreatePost(ctx);
                else if (path == "/api/forum/post/approve" && method == "POST")
                    HandleForumApprove(ctx);
                else if (path == "/api/forum/post/delete" && method == "POST")
                    HandleForumDelete(ctx);
                else if (path == "/api/report/create" && method == "POST")
                    HandleReportCreate(ctx);
                else if (path == "/api/report/list")
                    HandleReportList(ctx);
                else if (path == "/api/report/process" && method == "POST")
                    HandleReportProcess(ctx);
                else if (path == "/api/evidence/")
                    HandleEvidence(ctx);
                else
                {
                    resp.StatusCode = 404;
                    SendHtml(resp, WebPages.ErrorPage(404, "页面不存在"));
                }
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate] 请求处理: " + ex.Message);
                try { ctx.Response.StatusCode = 500; SendHtml(ctx.Response, WebPages.ErrorPage(500, "服务器内部错误")); }
                catch { }
            }
        }

        // ========== API 处理 ==========

        private void HandleLogin(HttpListenerContext ctx)
        {
            var form = ReadForm(ctx.Request);
            string user = GetForm(form, "username");
            string pass = GetForm(form, "password");

            string token = _auth.TryLogin(user, pass);
            if (token != null)
            {
                bool needPwd = _auth.IsAdminSession(token) && _auth.NeedPasswordChange();
                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["token"] = token,
                    ["isAdmin"] = _auth.IsAdminSession(token),
                    ["needPasswordChange"] = needPwd
                });
            }
            else
            {
                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = "用户名或密码错误"
                }, 401);
            }
        }

        private void HandleLogout(HttpListenerContext ctx)
        {
            string token = GetAuthToken(ctx.Request);
            _auth.Logout(token);
            SendJson(ctx.Response, new { success = true });
        }

        private void HandleMe(HttpListenerContext ctx)
        {
            string token = GetAuthToken(ctx.Request);
            if (string.IsNullOrEmpty(token) || !_auth.ValidateSession(token))
            {
                SendJson(ctx.Response, new { success = false, error = "未登录" }, 401);
                return;
            }

            string playerId;
            if (_auth.IsAdminSession(token))
            {
                // 管理员查看指定玩家或自己
                // 返回管理员信息
                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["isAdmin"] = true,
                    ["username"] = _config.AdminUser
                });
                return;
            }

            playerId = _auth.GetPlayerIdFromToken(token);
            if (playerId == null)
            {
                SendJson(ctx.Response, new { success = false, error = "无效token" }, 401);
                return;
            }

            var data = _data.GetPlayer(playerId);
            if (data == null)
            {
                SendJson(ctx.Response, new { success = false, error = "找不到玩家数据" });
                return;
            }

            SendJson(ctx.Response, new Dictionary<string, object>
            {
                ["success"] = true,
                ["isAdmin"] = false,
                ["player"] = data
            });
        }

        private void HandlePlayers(HttpListenerContext ctx)
        {
            string token = GetAuthToken(ctx.Request);
            bool isAdmin = _auth.IsAdminSession(token) && _auth.ValidateSession(token);

            if (!isAdmin && !_config.PublicLeaderboard)
            {
                SendJson(ctx.Response, new { success = false, error = "未授权" }, 403);
                return;
            }

            var players = _data.GetAllPlayers();
            var sorted = players.OrderByDescending(p => p.Level).ThenByDescending(p => p.Experience).ToList();

            SendJson(ctx.Response, new Dictionary<string, object>
            {
                ["success"] = true,
                ["players"] = sorted,
                ["total"] = sorted.Count
            });
        }

        private void HandleLeaderboard(HttpListenerContext ctx)
        {
            var players = _data.GetAllPlayers();
            var sorted = players.OrderByDescending(p => p.Level).ThenByDescending(p => p.Experience).Take(100).ToList();

            SendJson(ctx.Response, new Dictionary<string, object>
            {
                ["success"] = true,
                ["players"] = sorted,
                ["total"] = sorted.Count
            });
        }

        private void HandleLottery(HttpListenerContext ctx)
        {
            string token = GetAuthToken(ctx.Request);
            if (string.IsNullOrEmpty(token) || !_auth.ValidateSession(token))
            {
                SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未登录" }, 401);
                return;
            }

            bool isAdmin = _auth.IsAdminSession(token);
            string playerId = isAdmin ? null : _auth.GetPlayerIdFromToken(token);

            if (!isAdmin && string.IsNullOrEmpty(playerId))
            {
                SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "无效token" });
                return;
            }

            // 通过反射调用 ExperiencePlugin 的 LotteryManager 进行真实抽奖
            try
            {
                var expInstance = GetExpPluginInstance();
                if (expInstance == null)
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "无法连接到经验插件" });
                    return;
                }

                // 获取 Lottery 属性（LotteryManager 实例）
                var lotteryProp = expInstance.GetType().GetProperty("Lottery",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (lotteryProp == null)
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "找不到抽奖管理器" });
                    return;
                }
                var lotteryMgr = lotteryProp.GetValue(expInstance);
                if (lotteryMgr == null)
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "抽奖管理器未初始化" });
                    return;
                }

                // 调用 DrawOne() 方法抽奖
                var drawMethod = lotteryMgr.GetType().GetMethod("DrawOne",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (drawMethod == null)
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "抽奖方法不可用" });
                    return;
                }

                // 执行抽奖
                object prizeObj = drawMethod.Invoke(lotteryMgr, null);
                if (prizeObj == null)
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "抽奖结果为空" });
                    return;
                }

                // 读取 Prize 属性
                string prizeName = GetPrizeProperty(prizeObj, "Name");
                string prizeType = GetPrizeProperty(prizeObj, "Type");
                string prizeValueStr = GetPrizeProperty(prizeObj, "Value");
                string prizeDaysStr = GetPrizeProperty(prizeObj, "Days");
                int prizeValue = 0;
                int.TryParse(prizeValueStr, out prizeValue);
                int prizeDays = 0;
                int.TryParse(prizeDaysStr, out prizeDays);

                // 扣除积分并发放奖励（通过反射调用 DataManager）
                if (!isAdmin && playerId != null)
                {
                    DeductPoints(playerId, 5);

                    // 发放奖励
                    var dataMgrProp = expInstance.GetType().GetProperty("DataManager",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var dataMgr = dataMgrProp?.GetValue(expInstance);

                    switch (prizeType)
                    {
                        case "xp":
                            // 发放经验
                            var addExpMethod = dataMgr?.GetType().GetMethod("AddExperience",
                                new[] { typeof(string), typeof(int) });
                            addExpMethod?.Invoke(dataMgr, new object[] { playerId, prizeValue });
                            break;
                        case "points":
                            // 发放积分
                            var addPointsMethod = dataMgr?.GetType().GetMethod("AddPoints",
                                new[] { typeof(string), typeof(float) });
                            addPointsMethod?.Invoke(dataMgr, new object[] { playerId, (float)prizeValue });
                            break;
                        case "vip":
                        case "svip":
                            // 发放VIP/SVIP（调用 DataManager 的 SetVipLevel 或类似方法）
                            var setVipMethod = dataMgr?.GetType().GetMethod("SetVipLevel",
                                new[] { typeof(string), typeof(int), typeof(int) });
                            if (setVipMethod != null)
                            {
                                int vipLevel = prizeType == "svip" ? 2 : 1;
                                setVipMethod.Invoke(dataMgr, new object[] { playerId, vipLevel, prizeDays });
                            }
                            break;
                    }

                    // 通知在线玩家
                    var player = Player.Get(playerId);
                    if (player != null && player.IsConnected)
                    {
                        player.ShowHint(
                            "<size=16><color=#FFD700>🎉 抽奖结果</color></size>\n" +
                            "<size=14>获得: <color=#44FF88>" + prizeName + "</color></size>",
                            5f);
                    }
                }

                // 记录抽奖日志
                _store.AppendLotteryLog(new Dictionary<string, object>
                {
                    ["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    ["player"] = isAdmin ? "admin" : (_data.GetPlayer(playerId)?.Nickname ?? playerId),
                    ["prize"] = prizeName
                });

                // 获取剩余积分
                float remaining = isAdmin ? 99999 : (_data.GetPlayer(playerId)?.Points ?? 0);

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["prize"] = prizeName,
                    ["cost"] = 5,
                    ["remainingPoints"] = remaining
                });
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate-抽奖] " + ex.Message);
                SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "抽奖失败: " + ex.Message });
            }
        }

        /// <summary>通过反射读取 Prize 对象的属性</summary>
        private string GetPrizeProperty(object prizeObj, string propName)
        {
            try
            {
                var prop = prizeObj.GetType().GetProperty(propName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(prizeObj);
                    return val?.ToString() ?? "";
                }
            }
            catch { }
            return "";
        }

        /// <summary>通过反射扣除玩家积分</summary>
        private void DeductPoints(string userId, int amount)
        {
            try
            {
                var expInstance = GetExpPluginInstance();
                if (expInstance == null) return;

                var dataMgrProp = expInstance.GetType().GetProperty("DataManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var dataMgr = dataMgrProp?.GetValue(expInstance);
                if (dataMgr == null) return;

                var deductMethod = dataMgr.GetType().GetMethod("DeductPoints",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (deductMethod != null)
                    deductMethod.Invoke(dataMgr, new object[] { userId, (float)amount });
                else
                {
                    // 没有 DeductPoints 方法，用 AddPoints 负值
                    var addPointsMethod = dataMgr.GetType().GetMethod("AddPoints",
                        new[] { typeof(string), typeof(float) });
                    addPointsMethod?.Invoke(dataMgr, new object[] { userId, (float)(-amount) });
                }
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate] 扣积分失败: " + ex.Message);
            }
        }

        private void HandleRedeem(HttpListenerContext ctx)
        {
            var form = ReadForm(ctx.Request);
            string code = GetForm(form, "code");
            string token = GetAuthToken(ctx.Request);

            if (string.IsNullOrEmpty(code))
            {
                SendJson(ctx.Response, new { success = false, error = "请输入兑换码" });
                return;
            }

            string playerId = null;
            if (!string.IsNullOrEmpty(token) && _auth.ValidateSession(token))
            {
                if (_auth.IsAdminSession(token))
                    playerId = "admin"; // 管理员使用
                else
                    playerId = _auth.GetPlayerIdFromToken(token);
            }

            if (string.IsNullOrEmpty(playerId))
            {
                SendJson(ctx.Response, new { success = false, error = "请先登录" }, 401);
                return;
            }

            // 记录CDK使用记录并尝试执行兑换
            _store.AppendRedeemLog(playerId, code);
            Log.Info("[ExpWebBate] CDK兑换: 玩家 " + playerId + " 兑换 " + code);

            SendJson(ctx.Response, new Dictionary<string, object>
            {
                ["success"] = true,
                ["message"] = "兑换请求已记录，请在游戏内查看"
            });
        }

        private void HandleChangePassword(HttpListenerContext ctx)
        {
            var form = ReadForm(ctx.Request);
            string token = GetAuthToken(ctx.Request);
            string oldPwd = GetForm(form, "oldPassword");
            string newPwd = GetForm(form, "newPassword");

            if (string.IsNullOrEmpty(token) || !_auth.ValidateSession(token))
            {
                SendJson(ctx.Response, new { success = false, error = "未登录" }, 401);
                return;
            }

            if (!_auth.IsAdminSession(token))
            {
                SendJson(ctx.Response, new { success = false, error = "仅管理员可修改密码" }, 403);
                return;
            }

            if (string.IsNullOrEmpty(newPwd) || newPwd.Length < 4)
            {
                SendJson(ctx.Response, new { success = false, error = "密码长度至少4位" });
                return;
            }

            bool changed = _auth.ChangeAdminPassword(token, oldPwd, newPwd);
            if (changed)
            {
                _auth.Logout(token);
                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["message"] = "密码已修改，请重新登录"
                });
            }
            else
            {
                SendJson(ctx.Response, new { success = false, error = "旧密码错误" });
            }
        }

        // ========== 封禁处理 ==========

        private void HandleBan(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (!_auth.IsAdminSession(token) || !_auth.ValidateSession(token))
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未授权" }, 403);
                    return;
                }

                var form = ReadForm(ctx.Request);
                string targetId = GetForm(form, "targetId");    // UserId 或昵称
                string durationStr = GetForm(form, "duration"); // 小时数
                string reason = GetForm(form, "reason");
                string banType = GetForm(form, "banType");     // "game" = 游戏封禁, "community" = 社区封禁

                if (string.IsNullOrEmpty(targetId))
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "请输入玩家ID或昵称" });
                    return;
                }

                if (!int.TryParse(durationStr, out int hours) || hours <= 0)
                    hours = 24; // 默认24小时

                if (string.IsNullOrEmpty(reason))
                    reason = "管理员封禁";

                // 查找目标玩家
                Player targetPlayer = null;
                // 先按 UserId 精确查找（支持 steam/@steam）
                if (targetId.Contains("@"))
                    targetPlayer = Player.Get(targetId);
                // 再按昵称查找
                if (targetPlayer == null)
                    targetPlayer = Player.List.FirstOrDefault(p =>
                        p.Nickname.IndexOf(targetId, StringComparison.OrdinalIgnoreCase) >= 0);

                if (targetPlayer == null)
                {
                    // 尝试通过 ExperiencePlugin 数据查找
                    string userId = null;
                    try
                    {
                        var dataList = _data.GetAllPlayers();
                        var match = dataList.FirstOrDefault(d =>
                            d.Nickname.IndexOf(targetId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            d.UserId.IndexOf(targetId, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (match != null)
                            userId = match.UserId;
                    }
                    catch { }

                    if (userId == null)
                    {
                        SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未找到该玩家" });
                        return;
                    }

                    // 离线封禁：记录到封禁列表
                    Log.Info("[ExpWebBate-封禁] 离线玩家 " + userId + " " + hours + "小时: " + reason);
                    SendJson(ctx.Response, new Dictionary<string, object>
                    {
                        ["success"] = true,
                        ["message"] = "已记录离线封禁（下次加入时生效）"
                    });
                    return;
                }

                // 在线封禁
                if (banType == "community")
                {
                    // 社区封禁（仅禁用账号在Web面板的访问，不踢出游戏）
                    try
                    {
                        Log.Info("[ExpWebBate-封禁] 社区封禁 " + targetPlayer.Nickname + " " + hours + "小时: " + reason);
                        targetPlayer.ShowHint("<size=20><color=#FF4444>⚠ 你的社区账号已被封禁</color></size>\n原因: " + reason + "\n剩余: " + hours + "小时", 10);
                    }
                    catch { }
                }
                else
                {
                    // 游戏封禁（使用 Exiled 的 Player.Ban API）
                    try
                    {
                        var issuer = Exiled.API.Features.Server.Host;
                        targetPlayer.Ban(TimeSpan.FromHours(hours), reason, issuer);
                        Log.Info("[ExpWebBate-封禁] 游戏封禁 " + targetPlayer.Nickname + " " + hours + "小时: " + reason);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[ExpWebBate-封禁] Ban异常: " + ex.Message);
                        targetPlayer.Kick(reason);
                    }
                }

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["message"] = "已封禁 " + targetPlayer.Nickname + " " + hours + "小时"
                });
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate-封禁] HandleBan: " + ex.Message);
                SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message });
            }
        }

        private void HandleUnban(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (!_auth.IsAdminSession(token) || !_auth.ValidateSession(token))
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未授权" }, 403);
                    return;
                }

                var form = ReadForm(ctx.Request);
                string targetId = GetForm(form, "targetId");

                if (string.IsNullOrEmpty(targetId))
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "请输入玩家ID" });
                    return;
                }

                // 通过 Exiled BanManager 解除封禁
                try
                {
                    // 尝试解除UserId封禁和IP封禁
                    Exiled.API.Features.BanManager.UnbanPlayer(BanHandler.BanType.UserId, targetId);
                    Exiled.API.Features.BanManager.UnbanPlayer(BanHandler.BanType.IP, targetId);
                    Log.Info("[ExpWebBate-封禁] 解除封禁 " + targetId);
                }
                catch (Exception ex)
                {
                    Log.Error("[ExpWebBate-封禁] Unban异常: " + ex.Message);
                }

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["message"] = "已尝试解除 " + targetId + " 的封禁"
                });
            }
            catch (Exception ex)
            {
                Log.Error("[ExpWebBate-封禁] HandleUnban: " + ex.Message);
                SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message });
            }
        }

        // ========== 论坛 API ==========

        private void HandleForumPosts(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                bool isAdmin = _auth.IsAdminSession(token) && _auth.ValidateSession(token);

                var req = ctx.Request;
                int page = 1;
                int.TryParse(req.QueryString["page"], out page);
                if (page < 1) page = 1;

                bool onlyApproved = !isAdmin; // 管理员看所有，玩家只看已审核
                var posts = _forum.GetPosts(onlyApproved, page, 10);

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["posts"] = posts,
                    ["isAdmin"] = isAdmin
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        private void HandleForumPostView(HttpListenerContext ctx)
        {
            try
            {
                string postId = ctx.Request.QueryString["id"];
                if (string.IsNullOrEmpty(postId)) { Redirect(ctx.Response, "/forum"); return; }

                var post = _forum.GetPost(postId);
                if (post == null) { SendHtml(ctx.Response, WebPages.ErrorPage(404, "帖子不存在")); return; }

                post.Views++;
                string token = GetAuthToken(ctx.Request);
                bool isAdmin = _auth.IsAdminSession(token) && _auth.ValidateSession(token);
                bool isOwner = false;
                string sessionUser = GetSessionUser(ctx.Request);
                if (sessionUser != null)
                {
                    var playerData = _data.GetAllPlayers().FirstOrDefault(p => p.Nickname == sessionUser);
                    if (playerData != null && playerData.UserId == post.AuthorId)
                        isOwner = true;
                }

                SendHtml(ctx.Response, WebPages.ForumPostPage(post, isAdmin || isOwner));
            }
            catch (Exception ex) { SendHtml(ctx.Response, WebPages.ErrorPage(500, ex.Message)); }
        }

        private void HandleForumCreatePost(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (string.IsNullOrEmpty(token) || !_auth.ValidateSession(token))
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "请先登录" }, 401);
                    return;
                }

                var form = ReadForm(ctx.Request);
                string title = GetForm(form, "title");
                string content = GetForm(form, "content");
                string category = GetForm(form, "category");

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
                {
                    SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "标题和内容不能为空" });
                    return;
                }

                string userId = null;
                string userName = "匿名";
                if (_auth.IsAdminSession(token))
                {
                    userId = _config.AdminUser;
                    userName = _config.AdminUser;
                }
                else
                {
                    userId = _auth.GetPlayerIdFromToken(token);
                    var pd = _data.GetPlayer(userId);
                    if (pd != null) userName = pd.Nickname;
                }

                var post = new PostEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = title,
                    Content = content,
                    AuthorId = userId,
                    AuthorName = userName,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    Approved = false,
                    Category = string.IsNullOrEmpty(category) ? "general" : category,
                    Views = 0,
                    Replies = 0
                };

                _forum.CreatePost(post);
                Log.Info("[ExpWebBate-论坛] " + userName + " 发布帖子: " + title);

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["message"] = "帖子已提交，等待管理员审核"
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        private void HandleForumApprove(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (!_auth.IsAdminSession(token) || !_auth.ValidateSession(token))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未授权" }, 403); return; }

                var form = ReadForm(ctx.Request);
                string postId = GetForm(form, "postId");
                string action = GetForm(form, "action"); // "approve" or "reject"

                bool approved = action == "approve";
                bool ok = _forum.ApprovePost(postId, approved, "管理员");
                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = ok,
                    ["message"] = ok ? "操作成功" : "帖子不存在"
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        private void HandleForumDelete(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (!_auth.IsAdminSession(token) || !_auth.ValidateSession(token))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未授权" }, 403); return; }

                var form = ReadForm(ctx.Request);
                string postId = GetForm(form, "postId");

                bool ok = _forum.DeletePost(postId);
                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = ok,
                    ["message"] = ok ? "已删除" : "帖子不存在"
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        // ========== 举报 API ==========

        private void HandleReportCreate(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (string.IsNullOrEmpty(token) || !_auth.ValidateSession(token))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "请先登录" }, 401); return; }

                // 解析 multipart/form-data 或 urlencoded
                string targetId, targetName, reason, description, evidenceFile, evidenceUrl;

                if (ctx.Request.ContentType != null && ctx.Request.ContentType.Contains("multipart/form-data"))
                {
                    // 带文件上传的表单
                    var boundary = ctx.Request.ContentType.Split(';')[1].Trim().Replace("boundary=", "");
                    var ms = new MemoryStream();
                    ctx.Request.InputStream.CopyTo(ms);
                    byte[] body = ms.ToArray();
                    var parts = ParseMultipart(body, boundary);

                    targetId = GetMultipart(parts, "targetId");
                    targetName = GetMultipart(parts, "targetName");
                    reason = GetMultipart(parts, "reason");
                    description = GetMultipart(parts, "description");
                    evidenceUrl = GetMultipart(parts, "evidenceUrl");
                    evidenceFile = null;

                    // 处理文件上传
                    var fileData = GetMultipartFile(parts, "evidence");
                    if (fileData != null)
                    {
                        evidenceFile = _forum.SaveEvidence(
                            Convert.ToBase64String(fileData.Data),
                            fileData.FileName);
                    }
                }
                else
                {
                    var form = ReadForm(ctx.Request);
                    targetId = GetForm(form, "targetId");
                    targetName = GetForm(form, "targetName");
                    reason = GetForm(form, "reason");
                    description = GetForm(form, "description");
                    evidenceUrl = GetForm(form, "evidenceUrl");
                    evidenceFile = null;

                    // 如果是 base64 图片
                    string evidenceBase64 = GetForm(form, "evidenceBase64");
                    if (!string.IsNullOrEmpty(evidenceBase64))
                    {
                        string fileName = GetForm(form, "evidenceFileName");
                        if (string.IsNullOrEmpty(fileName)) fileName = "evidence.png";
                        evidenceFile = _forum.SaveEvidence(evidenceBase64, fileName);
                    }
                }

                if (string.IsNullOrEmpty(targetId))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "请输入被举报玩家" }); return; }

                if (string.IsNullOrEmpty(evidenceUrl) && string.IsNullOrEmpty(evidenceFile))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "必须上传图片/视频证据或提供证据链接" }); return; }

                string reporterId = null;
                string reporterName = "匿名";
                if (_auth.IsAdminSession(token))
                { reporterId = _config.AdminUser; reporterName = "管理员"; }
                else
                { reporterId = _auth.GetPlayerIdFromToken(token);
                    var pd = _data.GetPlayer(reporterId);
                    if (pd != null) reporterName = pd.Nickname; }

                var report = new ReportEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ReporterId = reporterId,
                    ReporterName = reporterName,
                    TargetId = targetId,
                    TargetName = string.IsNullOrEmpty(targetName) ? targetId : targetName,
                    Reason = reason,
                    Description = description,
                    EvidenceUrl = evidenceUrl,
                    EvidenceFile = evidenceFile,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    Status = "pending",
                    ServerInfo = "ExpWebBate"
                };

                _forum.CreateReport(report);
                Log.Info("[ExpWebBate-举报] " + reporterName + " 举报 " + targetName + ": " + reason);

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["message"] = "举报已提交，等待管理员处理"
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        private void HandleReportList(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (!_auth.IsAdminSession(token) || !_auth.ValidateSession(token))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未授权" }, 403); return; }

                var req = ctx.Request;
                int page = 1;
                int.TryParse(req.QueryString["page"], out page);
                if (page < 1) page = 1;
                string filter = req.QueryString["filter"];

                bool onlyPending = filter == "pending";
                var reports = _forum.GetReports(onlyPending, page, 20);

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["reports"] = reports,
                    ["total"] = reports.Count
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        private void HandleReportProcess(HttpListenerContext ctx)
        {
            try
            {
                string token = GetAuthToken(ctx.Request);
                if (!_auth.IsAdminSession(token) || !_auth.ValidateSession(token))
                { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = "未授权" }, 403); return; }

                var form = ReadForm(ctx.Request);
                string reportId = GetForm(form, "reportId");
                string action = GetForm(form, "action"); // "approved" or "rejected"

                bool ok = _forum.ProcessReport(reportId, action, "管理员");

                // 如果批准封禁，同时执行封禁
                if (ok && action == "approved")
                {
                    var report = _forum.GetReport(reportId);
                    if (report != null)
                    {
                        var targetPlayer = Player.Get(report.TargetId);
                        if (targetPlayer != null)
                        {
                            try
                            {
                                targetPlayer.Ban(TimeSpan.FromDays(7), report.Reason, Exiled.API.Features.Server.Host);
                            }
                            catch { }
                        }
                    }
                }

                SendJson(ctx.Response, new Dictionary<string, object>
                {
                    ["success"] = ok,
                    ["message"] = ok ? "处理成功" : "举报不存在"
                });
            }
            catch (Exception ex) { SendJson(ctx.Response, new Dictionary<string, object> { ["success"] = false, ["error"] = ex.Message }); }
        }

        private void HandleEvidence(HttpListenerContext ctx)
        {
            try
            {
                string file = ctx.Request.QueryString["file"];
                if (string.IsNullOrEmpty(file)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }

                byte[] data = _forum.GetEvidence(file);
                if (data == null) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }

                string ext = Path.GetExtension(file)?.ToLower();
                string mime = "application/octet-stream";
                if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
                else if (ext == ".png") mime = "image/png";
                else if (ext == ".gif") mime = "image/gif";
                else if (ext == ".mp4") mime = "video/mp4";
                else if (ext == ".webm") mime = "video/webm";

                ctx.Response.ContentType = mime;
                ctx.Response.ContentLength64 = data.Length;
                ctx.Response.OutputStream.Write(data, 0, data.Length);
                ctx.Response.Close();
            }
            catch { ctx.Response.StatusCode = 500; ctx.Response.Close(); }
        }

        // ========== Multipart 解析辅助 ==========

        private class MultipartPart
        {
            public Dictionary<string, string> Headers { get; set; }
            public byte[] Data { get; set; }
            public string Name => Headers != null && Headers.ContainsKey("name") ? Headers["name"] : "";
            public string FileName => Headers != null && Headers.ContainsKey("filename") ? Headers["filename"] : "";
        }

        private List<MultipartPart> ParseMultipart(byte[] body, string boundary)
        {
            var parts = new List<MultipartPart>();
            if (body == null || body.Length == 0) return parts;

            byte[] delimiter = Encoding.ASCII.GetBytes("--" + boundary);
            byte[] endDelimiter = Encoding.ASCII.GetBytes("--" + boundary + "--");

            int pos = 0;
            while (pos < body.Length)
            {
                // 查找分隔符
                int start = IndexOf(body, delimiter, pos);
                if (start < 0) break;
                start += delimiter.Length;

                // 跳过 \r\n
                while (start < body.Length && (body[start] == '\r' || body[start] == '\n')) start++;

                // 检查是否是结束分隔符
                if (pos > 0 && IndexOf(body, endDelimiter, pos - delimiter.Length) >= 0) break;

                // 找下一个分隔符
                int next = IndexOf(body, delimiter, start);
                if (next < 0) next = body.Length;

                // 找头部和内容的空行
                int headerEnd = IndexOf(body, Encoding.ASCII.GetBytes("\r\n\r\n"), start);
                if (headerEnd < 0) break;

                byte[] headerBytes = new byte[headerEnd - start];
                Array.Copy(body, start, headerBytes, 0, headerEnd - start);
                string headerStr = Encoding.ASCII.GetString(headerBytes);

                int contentStart = headerEnd + 4;
                int contentLen = next - contentStart;
                // 去掉尾部 \r\n
                if (contentLen >= 2 && body[contentStart + contentLen - 2] == '\r' && body[contentStart + contentLen - 1] == '\n')
                    contentLen -= 2;

                byte[] contentData = new byte[contentLen];
                Array.Copy(body, contentStart, contentData, 0, contentLen);

                var part = new MultipartPart { Headers = new Dictionary<string, string>(), Data = contentData };
                // 解析 Content-Disposition
                foreach (string line in headerStr.Split('\n'))
                {
                    string l = line.Trim();
                    if (l.StartsWith("Content-Disposition:"))
                    {
                        var dis = l.Substring("Content-Disposition:".Length).Trim();
                        foreach (string kv in dis.Split(';'))
                        {
                            string kvt = kv.Trim();
                            if (kvt.Contains("="))
                            {
                                var kvp = kvt.Split(new[] { '=' }, 2);
                                string k = kvp[0].Trim();
                                string v = kvp[1].Trim().Trim('"');
                                part.Headers[k] = v;
                            }
                        }
                    }
                }
                parts.Add(part);
                pos = next;
            }
            return parts;
        }

        private string GetMultipart(List<MultipartPart> parts, string name)
        {
            var part = parts.FirstOrDefault(p => p.Name == name);
            if (part == null) return null;
            return Encoding.UTF8.GetString(part.Data);
        }

        private MultipartPart GetMultipartFile(List<MultipartPart> parts, string name)
        {
            return parts.FirstOrDefault(p => p.Name == name && !string.IsNullOrEmpty(p.FileName));
        }

        private static int IndexOf(byte[] data, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        // ========== 工具方法 ==========

        private string GetAuthToken(HttpListenerRequest req)
        {
            // 从 Cookie 或 Authorization header 获取
            var cookie = req.Cookies["session"];
            if (cookie != null) return cookie.Value;

            string auth = req.Headers["Authorization"];
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer "))
                return auth.Substring(7);

            // 从 Query 参数获取
            string qToken = req.QueryString["token"];
            if (!string.IsNullOrEmpty(qToken)) return qToken;

            return null;
        }

        private string GetSessionUser(HttpListenerRequest req)
        {
            string token = GetAuthToken(req);
            if (string.IsNullOrEmpty(token) || !_auth.ValidateSession(token))
                return null;

            if (_auth.IsAdminSession(token))
                return _config.AdminUser;

            string pid = _auth.GetPlayerIdFromToken(token);
            if (pid != null)
            {
                var data = _data.GetPlayer(pid);
                return data?.Nickname ?? pid;
            }
            return null;
        }

        private static Dictionary<string, string> ReadForm(HttpListenerRequest req)
        {
            var result = new Dictionary<string, string>();
            try
            {
                using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    if (string.IsNullOrEmpty(body)) return result;
                    foreach (string pair in body.Split('&'))
                    {
                        if (string.IsNullOrEmpty(pair)) continue;
                        var kv = pair.Split('=');
                        if (kv.Length >= 2)
                            result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                        else if (kv.Length == 1)
                            result[Uri.UnescapeDataString(kv[0])] = "";
                    }
                }
            }
            catch { }
            return result;
        }

        private static string GetForm(Dictionary<string, string> form, string key)
        {
            return form.ContainsKey(key) ? form[key] : "";
        }

        private static void SendHtml(HttpListenerResponse resp, string html)
        {
            byte[] buf = Encoding.UTF8.GetBytes(html);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static void SendJson(HttpListenerResponse resp, object data, int status = 200)
        {
            string json;
            if (data is Dictionary<string, object> dict)
                json = DictToJson(dict);
            else
                json = SimpleJson(data);

            byte[] buf = Encoding.UTF8.GetBytes(json);
            resp.StatusCode = status;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static void Redirect(HttpListenerResponse resp, string url)
        {
            resp.StatusCode = 302;
            resp.Headers.Add("Location", url);
            resp.Close();
        }

        private static string DictToJson(Dictionary<string, object> dict)
        {
            var parts = new List<string>();
            foreach (var kvp in dict)
            {
                string val;
                if (kvp.Value == null)
                    val = "null";
                else if (kvp.Value is string || kvp.Value is PlayerDataEntry)
                    val = "\"" + JsonEscape(kvp.Value.ToString()) + "\"";
                else if (kvp.Value is bool b)
                    val = b ? "true" : "false";
                else if (kvp.Value is int || kvp.Value is long || kvp.Value is float || kvp.Value is double)
                    val = kvp.Value.ToString().ToLower();
                else if (kvp.Value is List<PlayerDataEntry> list)
                    val = ListToJson(list);
                else
                    val = "\"" + JsonEscape(kvp.Value.ToString()) + "\"";
                parts.Add("\"" + JsonEscape(kvp.Key) + "\":" + val);
            }
            return "{" + string.Join(",", parts) + "}";
        }

        private static string ListToJson(List<PlayerDataEntry> list)
        {
            var items = new List<string>();
            foreach (var entry in list)
            {
                var dict = new Dictionary<string, object>
                {
                    ["userId"] = entry.UserId,
                    ["nickname"] = entry.Nickname,
                    ["level"] = entry.Level,
                    ["exp"] = entry.Experience,
                    ["points"] = entry.Points,
                    ["vipLevel"] = entry.VipLevel,
                    ["kills"] = entry.TotalKills,
                    ["deaths"] = entry.TotalDeaths,
                    ["isOnline"] = entry.IsOnline,
                    ["role"] = entry.CurrentRole ?? ""
                };
                items.Add(DictToJson(dict));
            }
            return "[" + string.Join(",", items) + "]";
        }

        private static string SimpleJson(object obj)
        {
            if (obj == null) return "null";
            return DictToJson(new Dictionary<string, object>());
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
