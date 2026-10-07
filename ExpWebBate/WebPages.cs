using System;

namespace ExpWebBate
{
    public static class WebPages
    {
        private static string HtmlBase = "<!DOCTYPE html><html lang=\"zh-CN\"><head>" +
            "<meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<title>{TITLE} - ExpWebBate</title>" +
            "<style>" +
            "*{margin:0;padding:0;box-sizing:border-box}" +
            "body{font-family:'Segoe UI',Arial,sans-serif;background:linear-gradient(135deg,#0f0c29,#302b63,#24243e);color:#fff;min-height:100vh}" +
            "nav{background:rgba(0,0,0,0.5);padding:12px 20px;backdrop-filter:blur(10px);border-bottom:1px solid rgba(255,255,255,0.1)}" +
            "nav a{color:#88ddff;text-decoration:none;margin-right:20px;font-size:14px;transition:color .3s}" +
            "nav a:hover{color:#fff}" +
            ".container{max-width:1000px;margin:0 auto;padding:20px}" +
            ".card{background:rgba(255,255,255,0.08);border-radius:12px;padding:25px;margin-bottom:20px;backdrop-filter:blur(10px);border:1px solid rgba(255,255,255,0.1)}" +
            "h1{font-size:24px;margin-bottom:15px;background:linear-gradient(90deg,#667eea,#764ba2);-webkit-background-clip:text;-webkit-text-fill-color:transparent}" +
            "h2{font-size:18px;margin-bottom:12px;color:#ddd}" +
            "input,select,button{width:100%;padding:12px 15px;margin:8px 0;border:none;border-radius:8px;font-size:14px;outline:none;background:rgba(255,255,255,0.1);color:#fff;transition:all .3s}" +
            "input:focus{background:rgba(255,255,255,0.2);box-shadow:0 0 10px rgba(102,126,234,0.5)}" +
            "button{background:linear-gradient(90deg,#667eea,#764ba2);color:#fff;cursor:pointer;font-weight:bold}" +
            "button:hover{transform:translateY(-2px);box-shadow:0 5px 20px rgba(102,126,234,0.4)}" +
            "button:disabled{opacity:0.5;cursor:not-allowed;transform:none}" +
            ".error{color:#ff6b6b;font-size:13px;margin-top:5px}" +
            ".success{color:#51cf66;font-size:13px;margin-top:5px}" +
            "table{width:100%;border-collapse:collapse;margin-top:10px}" +
            "th,td{padding:10px 8px;text-align:left;border-bottom:1px solid rgba(255,255,255,0.1);font-size:13px}" +
            "th{color:#88ddff;font-weight:normal}" +
            "tr:hover{background:rgba(255,255,255,0.05)}" +
            ".badge{display:inline-block;padding:2px 8px;border-radius:4px;font-size:11px;font-weight:bold}" +
            ".badge-vip{background:linear-gradient(90deg,#FFD700,#FFA500);color:#000}" +
            ".badge-svip{background:linear-gradient(90deg,#FF69B4,#FF1493);color:#fff}" +
            ".badge-offline{background:#555;color:#aaa}" +
            ".badge-online{background:#51cf66;color:#fff}" +
            ".loading{text-align:center;padding:40px;color:#aaa;font-size:14px}" +
            ".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:15px}" +
            ".stat-card{background:rgba(255,255,255,0.05);border-radius:10px;padding:15px;text-align:center}" +
            ".stat-card .num{font-size:28px;font-weight:bold;background:linear-gradient(90deg,#667eea,#764ba2);-webkit-background-clip:text;-webkit-text-fill-color:transparent}" +
            ".stat-card .label{font-size:12px;color:#aaa;margin-top:5px}" +
            ".fade-in{animation:fadeIn .5s}" +
            "@keyframes fadeIn{from{opacity:0;transform:translateY(20px)}to{opacity:1;transform:translateY(0)}}" +
            ".hidden{display:none}" +
            "footer{text-align:center;padding:20px;color:#555;font-size:12px}" +
            ".rank{font-size:12px;color:#aaa}" +
            "</style></head><body>{NAV}<div class=\"container\">{CONTENT}</div>" +
            "<script>" +
            "function getCookie(n){var c=document.cookie.match('(^|; )'+n+'=([^;]*)');return c?c[2]:''}" +
            "function setCookie(n,v,d){var x=new Date();x.setTime(x.getTime()+d*24*3600000);document.cookie=n+'='+v+';path=/;expires='+x.toUTCString()}" +
            "function delCookie(n){document.cookie=n+'=;path=/;expires=Thu,01 Jan 1970 00:00:00 GMT'}" +
            "async function api(url,opts){try{var r=await fetch(url,{headers:{'Authorization':'Bearer '+getCookie('session')},...opts});return await r.json()}catch(e){return{success:false,error:e.message}}}" +
            "function logout(){delCookie('session');window.location.href='/login'}" +
            "</script><footer>ExpWebBate v1.0 - ExperiencePlugin Web UI</footer></body></html>";

        private static string Layout(string title, string content, bool loggedIn = false, string userName = null)
        {
            string nav = loggedIn
                ? "<nav><a href=\"/dashboard\">数据</a><a href=\"/lottery\">抽奖</a><a href=\"/redeem\">兑换</a><a href=\"/forum\">论坛</a><a href=\"/report\">举报</a><a href=\"/admin\">管理</a><span style=\"color:#aaa;float:right;margin-right:15px\">"
                  + HtmlEscape(userName) + " <a href=\"/api/logout\" onclick=\"logout()\" style=\"color:#ff6;font-size:12px\">退出</a></span></nav>"
                : "";

            return HtmlBase
                .Replace("{TITLE}", HtmlEscape(title))
                .Replace("{NAV}", nav)
                .Replace("{CONTENT}", content);
        }

        public static string LoginPage()
        {
            return Layout("登录", @"
<div class=""fade-in"" style=""max-width:420px;margin:80px auto"">
<div class=""card"">
<h1 style=""text-align:center;font-size:28px"">🎫 ExpWebBate</h1>
<p style=""text-align:center;color:#aaa;margin-bottom:20px;font-size:13px"">ExperiencePlugin Web 管理面板</p>

<div style=""margin-bottom:25px"">
<div style=""display:flex;gap:10px;margin-bottom:15px"">
<button onclick=""switchTab('admin')"" id=""tabAdmin"" style=""flex:1;background:rgba(102,126,234,0.3);border:2px solid rgba(102,126,234,0.5)"">🔐 管理员</button>
<button onclick=""switchTab('player')"" id=""tabPlayer"" style=""flex:1;background:rgba(255,255,255,0.05);border:2px solid rgba(255,255,255,0.1)"">🎮 玩家</button>
</div>

<div id=""adminTab"">
<p style=""font-size:13px;color:#888;margin-bottom:5px"">管理员账号</p>
<input type=""text"" id=""username"" placeholder=""admin"" value=""admin"">
<p style=""font-size:13px;color:#888;margin-bottom:5px"">密码</p>
<input type=""password"" id=""password"" placeholder=""密码"">
</div>

<div id=""playerTab"" style=""display:none"">
<p style=""font-size:13px;color:#888;margin-bottom:5px"">6位绑定码</p>
<input type=""text"" id=""bindCode"" placeholder=""输入游戏内的6位绑定码"" maxlength=""6"" style=""text-align:center;font-size:22px;letter-spacing:4px"">
<p style=""font-size:12px;color:#666;margin-top:5px"">在游戏中输入 <color=yellow>.bind</color> 获取绑定码</p>
</div>
</div>

<button onclick=""login()"" id=""loginBtn"">登录</button>
<div id=""loginError"" class=""error hidden""></div>
</div>
</div>
<script>
function switchTab(tab){if(tab==='admin'){document.getElementById('tabAdmin').style.background='rgba(102,126,234,0.3)';document.getElementById('tabAdmin').style.border='2px solid rgba(102,126,234,0.5)';document.getElementById('tabPlayer').style.background='rgba(255,255,255,0.05)';document.getElementById('tabPlayer').style.border='2px solid rgba(255,255,255,0.1)';document.getElementById('adminTab').style.display='block';document.getElementById('playerTab').style.display='none'}else{document.getElementById('tabPlayer').style.background='rgba(102,126,234,0.3)';document.getElementById('tabPlayer').style.border='2px solid rgba(102,126,234,0.5)';document.getElementById('tabAdmin').style.background='rgba(255,255,255,0.05)';document.getElementById('tabAdmin').style.border='2px solid rgba(255,255,255,0.1)';document.getElementById('adminTab').style.display='none';document.getElementById('playerTab').style.display='block'}}
async function login(){var tab=document.getElementById('adminTab').style.display!=='none'?'admin':'player';var u,p;if(tab==='admin'){u=document.getElementById('username').value;p=document.getElementById('password').value}else{u='player';p=document.getElementById('bindCode').value}
if(!p){document.getElementById('loginError').textContent='请输入'+(tab==='admin'?'密码':'绑定码');document.getElementById('loginError').classList.remove('hidden');return}
var btn=document.getElementById('loginBtn');btn.disabled=true;btn.textContent='登录中...';var r=await fetch('/api/login',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:'username='+encodeURIComponent(u)+'&password='+encodeURIComponent(p)});var d=await r.json();if(d.success){setCookie('session',d.token,1);if(d.needPasswordChange){window.location.href='/admin'}else{window.location.href='/dashboard'}}else{document.getElementById('loginError').textContent='登录失败，请检查信息';document.getElementById('loginError').classList.remove('hidden')}btn.disabled=false;btn.textContent='登录'}
document.getElementById('password').addEventListener('keydown',function(e){if(e.key==='Enter')login()});
document.getElementById('bindCode').addEventListener('keydown',function(e){if(e.key==='Enter')login()});
</script>", false);
        }

        public static string DashboardPage(string userName)
        {
            return Layout("数据面板",
@"<div class=""fade-in"">
<div class=""card"">
<h1>📊 玩家数据</h1>
<p style=""color:#aaa;font-size:13px"">实时读取 ExperiencePlugin 数据</p>
<div id=""loading"" class=""loading"">加载中...</div>
<div id=""dashboard"" class=""hidden"">
<div class=""grid"" id=""statsGrid""></div>
<div style=""margin-top:20px"">
<h2>🏆 排行榜 Top100</h2>
<div style=""overflow-x:auto""><table><thead><tr><th>#</th><th>玩家</th><th>等级</th><th>经验</th><th>积分</th><th>VIP</th><th>击杀</th><th>状态</th></tr></thead><tbody id=""leaderboard""></tbody></table></div>
</div>
</div>
</div>
</div>
<script>
(async function(){try{var d=await api('/api/players');var me=await api('/api/me');if(d.success){document.getElementById('loading').classList.add('hidden');document.getElementById('dashboard').classList.remove('hidden');
var grid=document.getElementById('statsGrid');grid.innerHTML='';
var s=[{l:'在线人数',n:d.players.filter(p=>p.isOnline).length},{l:'总玩家',n:d.total},{l:'最高等级',n:Math.max(...d.players.map(p=>p.level))},{l:'总击杀',n:d.players.reduce((a,p)=>a+p.kills,0)}];
s.forEach(function(x){grid.innerHTML+='<div class=""stat-card""><div class=""num"">'+x.n+'</div><div class=""label"">'+x.l+'</div></div>'});
var tb=document.getElementById('leaderboard');
var sorted=d.players.sort((a,b)=>b.level-a.level||b.exp-a.exp).slice(0,100);
sorted.forEach(function(p,i){var vip=p.vipLevel>=2?'<span class=""badge badge-svip"">SVIP</span>':p.vipLevel>=1?'<span class=""badge badge-vip"">VIP</span>':'<span class=""badge"">普通</span>';
var status=p.isOnline?'<span class=""badge badge-online"">在线</span>':'<span class=""badge badge-offline"">离线</span>';
tb.innerHTML+='<tr><td class=""rank"">'+(i+1)+'</td><td>'+p.nickname+' <span style=""font-size:11px;color:#666"">('+p.userId.substring(0,8)+')</span></td><td>'+p.level+'</td><td>'+p.exp+'</td><td>'+p.points+'</td><td>'+vip+'</td><td>'+p.kills+'</td><td>'+status+'</td></tr>'})}}catch(e){document.getElementById('loading').textContent='加载失败: '+e.message}})()
</script>", userName != null, userName);
        }

        public static string LotteryPage(string userName)
        {
            return Layout("抽奖",
@"<div class=""fade-in"">
<div class=""card"" style=""max-width:500px;margin:60px auto;text-align:center"">
<h1>🎰 积分抽奖</h1>
<p style=""color:#aaa;font-size:13px;margin-bottom:20px"">每次消耗 <strong style=""color:#FFD700"">5 积分</strong></p>
<div id=""lotteryResult"" class=""hidden"" style=""margin:20px 0;padding:20px;background:rgba(102,126,234,0.2);border-radius:10px;font-size:18px""></div>
<button onclick=""startLottery()"" id=""lotteryBtn"" style=""font-size:16px;padding:15px"">🎲 抽奖 (5积分)</button>
<div id=""lotteryError"" class=""error hidden""></div>
<div style=""margin-top:20px;padding:15px;background:rgba(0,0,0,0.3);border-radius:8px"">
<p style=""font-size:12px;color:#888"">可获奖品：100经验、50经验、200积分、SCP207、复活币、提示卡</p>
</div>
</div>
</div>
<script>
async function startLottery(){var btn=document.getElementById('lotteryBtn');btn.disabled=true;btn.textContent='抽奖中...';document.getElementById('lotteryResult').classList.add('hidden');var d=await api('/api/lottery');if(d.success){document.getElementById('lotteryResult').textContent='🎉 获得: '+d.prize;document.getElementById('lotteryResult').classList.remove('hidden');document.getElementById('lotteryResult').style.background='rgba(81,207,102,0.2)'}else{document.getElementById('lotteryError').textContent=d.error;document.getElementById('lotteryError').classList.remove('hidden')}btn.disabled=false;btn.textContent='🎲 抽奖 (5积分)'}
</script>", userName != null, userName);
        }

        public static string RedeemPage(string userName)
        {
            return Layout("兑换CDK",
@"<div class=""fade-in"">
<div class=""card"" style=""max-width:500px;margin:60px auto;text-align:center"">
<h1>🎫 CDK 兑换</h1>
<p style=""color:#aaa;font-size:13px;margin-bottom:20px"">输入激活码兑换 VIP / SVIP</p>
<input type=""text"" id=""cdkCode"" placeholder=""输入CDK兑换码"" style=""text-align:center;font-size:18px;letter-spacing:2px"">
<button onclick=""redeem()"" id=""redeemBtn"">兑换</button>
<div id=""redeemResult"" class=""hidden"" style=""margin-top:15px""></div>
</div>
</div>
<script>
async function redeem(){var code=document.getElementById('cdkCode').value.trim();if(!code)return;var btn=document.getElementById('redeemBtn');btn.disabled=true;btn.textContent='处理中...';var r=await fetch('/api/redeem',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'code='+encodeURIComponent(code)});var d=await r.json();var el=document.getElementById('redeemResult');el.classList.remove('hidden');if(d.success){el.innerHTML='<div class=""success"">'+d.message+'</div>';document.getElementById('cdkCode').value=''}else{el.innerHTML='<div class=""error"">'+d.error+'</div>'}btn.disabled=false;btn.textContent='兑换'}
document.getElementById('cdkCode').addEventListener('keydown',function(e){if(e.key==='Enter')redeem()});
</script>", userName != null, userName);
        }

        public static string AdminPage(string token, bool needPasswordChange)
        {
            string pwdWarning = needPasswordChange
                ? "<div style=\"background:rgba(255,107,107,0.2);border:1px solid #ff6b6b;border-radius:8px;padding:15px;margin-bottom:20px\"><strong style=\"color:#ff6b6b\">⚠ 首次登录请立即修改默认密码！</strong></div>"
                : "";

            return Layout("管理",
@"<div class=""fade-in"">
<div class=""card"" style=""max-width:500px;margin:60px auto"">
<h1>⚙ 管理员设置</h1>{PWD_WARNING}
<div>
<h2>🔑 修改密码</h2>
<input type=""password"" id=""oldPwd"" placeholder=""当前密码"">
<input type=""password"" id=""newPwd"" placeholder=""新密码(至少4位)"">
<button onclick=""changePwd()"" id=""pwdBtn"">修改密码</button>
<div id=""pwdResult"" class=""hidden""></div>
</div>
<hr style=""border:none;border-top:1px solid rgba(255,255,255,0.1);margin:20px 0"">
<div>
<h2>📊 系统状态</h2>
<div class=""stat-card""><div class=""num"" id=""playerCount"">-</div><div class=""label"">在线玩家</div></div>
</div>
<hr style=""border:none;border-top:1px solid rgba(255,255,255,0.1);margin:20px 0"">
<div style=""text-align:center"">
<a href=""/ban"" style=""display:inline-block;padding:12px 25px;background:linear-gradient(90deg,#ff4444,#cc0000);color:#fff;border-radius:8px;text-decoration:none;font-size:15px"">🚫 玩家封禁管理</a>
</div>
</div>
</div>
<script>
async function changePwd(){var o=document.getElementById('oldPwd').value;var n=document.getElementById('newPwd').value;if(!n||n.length<4){document.getElementById('pwdResult').innerHTML='<div class=""error"">密码至少4位</div>';document.getElementById('pwdResult').classList.remove('hidden');return}
var r=await fetch('/api/admin/changepwd',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'oldPassword='+encodeURIComponent(o)+'&newPassword='+encodeURIComponent(n)});var d=await r.json();var el=document.getElementById('pwdResult');el.classList.remove('hidden');if(d.success){el.innerHTML='<div class=""success"">'+d.message+'</div>';setTimeout(function(){logout()},2000)}else{el.innerHTML='<div class=""error"">'+d.error+'</div>'}}
(async function(){try{var d=await api('/api/players');if(d.success)document.getElementById('playerCount').textContent=d.players.filter(p=>p.isOnline).length}catch(e){}})()
</script>", true, "管理员")
                .Replace("{PWD_WARNING}", pwdWarning);
        }

        public static string BanPage(string token)
        {
            return Layout("封禁管理",
@"<div class=""fade-in"">
<div class=""card"" style=""max-width:600px;margin:40px auto"">
<h1>🚫 玩家封禁管理</h1>

<div style=""margin-bottom:20px;display:flex;gap:10px"">
<button onclick=""switchBanTab('ban')"" id=""banTabBtn"" style=""flex:1;background:rgba(255,68,68,0.3);border:2px solid rgba(255,68,68,0.5)"">封禁玩家</button>
<button onclick=""switchBanTab('unban')"" id=""unbanTabBtn"" style=""flex:1;background:rgba(255,255,255,0.05);border:2px solid rgba(255,255,255,0.1)"">解除封禁</button>
</div>

<div id=""banTab"">
<h2>🔒 封禁玩家</h2>
<p style=""font-size:13px;color:#888;margin-bottom:10px"">封禁后将无法进入服务器</p>

<label style=""font-size:13px;color:#aaa"">玩家ID或昵称</label>
<input type=""text"" id=""banTarget"" placeholder=""输入SteamId或昵称..."" style=""margin-bottom:5px"">

<label style=""font-size:13px;color:#aaa"">封禁时长（小时）</label>
<input type=""number"" id=""banDuration"" value=""24"" min=""1"" max=""8760"" style=""margin-bottom:5px"">

<label style=""font-size:13px;color:#aaa"">原因</label>
<input type=""text"" id=""banReason"" placeholder=""违反服务器规则"" value=""违反服务器规则"">

<label style=""font-size:13px;color:#aaa;margin-top:10px;display:block"">封禁类型</label>
<div style=""display:flex;gap:10px;margin:8px 0"">
<button onclick=""setBanType('game')"" id=""banTypeGame"" style=""flex:1;background:rgba(255,68,68,0.3);border:2px solid #ff4444;padding:10px"">🎮 游戏封禁</button>
<button onclick=""setBanType('community')"" id=""banTypeCommunity"" style=""flex:1;background:rgba(255,255,255,0.05);border:2px solid rgba(255,255,255,0.1);padding:10px"">🌐 社区封禁</button>
</div>
<p style=""font-size:12px;color:#666"">游戏封禁: 踢出游戏并禁止加入<br>社区封禁: 仅禁用Web面板访问</p>

<button onclick=""banPlayer()"" id=""banBtn"" style=""background:linear-gradient(90deg,#ff4444,#cc0000);margin-top:10px"">🚫 执行封禁</button>
<div id=""banResult"" class=""hidden""></div>
</div>

<div id=""unbanTab"" style=""display:none"">
<h2>🔓 解除封禁</h2>
<label style=""font-size:13px;color:#aaa"">玩家SteamId</label>
<input type=""text"" id=""unbanTarget"" placeholder=""7656119...@steam"">
<button onclick=""unbanPlayer()"" id=""unbanBtn"" style=""background:linear-gradient(90deg,#51cf66,#2b8a3e);margin-top:10px"">🔓 解除封禁</button>
<div id=""unbanResult"" class=""hidden""></div>
</div>
</div>
</div>
<script>
var banType='game';
function setBanType(t){banType=t;document.getElementById('banTypeGame').style.background=t==='game'?'rgba(255,68,68,0.3)':'rgba(255,255,255,0.05)';document.getElementById('banTypeGame').style.border=t==='game'?'2px solid #ff4444':'2px solid rgba(255,255,255,0.1)';document.getElementById('banTypeCommunity').style.background=t==='community'?'rgba(255,68,68,0.3)':'rgba(255,255,255,0.05)';document.getElementById('banTypeCommunity').style.border=t==='community'?'2px solid #ff4444':'2px solid rgba(255,255,255,0.1)'}
function switchBanTab(t){if(t==='ban'){document.getElementById('banTabBtn').style.background='rgba(255,68,68,0.3)';document.getElementById('banTabBtn').style.border='2px solid rgba(255,68,68,0.5)';document.getElementById('unbanTabBtn').style.background='rgba(255,255,255,0.05)';document.getElementById('unbanTabBtn').style.border='2px solid rgba(255,255,255,0.1)';document.getElementById('banTab').style.display='block';document.getElementById('unbanTab').style.display='none'}else{document.getElementById('unbanTabBtn').style.background='rgba(255,68,68,0.3)';document.getElementById('unbanTabBtn').style.border='2px solid rgba(255,68,68,0.5)';document.getElementById('banTabBtn').style.background='rgba(255,255,255,0.05)';document.getElementById('banTabBtn').style.border='2px solid rgba(255,255,255,0.1)';document.getElementById('banTab').style.display='none';document.getElementById('unbanTab').style.display='block'}}
async function banPlayer(){var target=document.getElementById('banTarget').value.trim();var dur=document.getElementById('banDuration').value;var reason=document.getElementById('banReason').value.trim();if(!target){document.getElementById('banResult').innerHTML='<div class=""error"">请输入玩家</div>';document.getElementById('banResult').classList.remove('hidden');return}
var btn=document.getElementById('banBtn');btn.disabled=true;btn.textContent='执行中...';var r=await fetch('/api/ban',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'targetId='+encodeURIComponent(target)+'&duration='+encodeURIComponent(dur)+'&reason='+encodeURIComponent(reason)+'&banType='+banType});var d=await r.json();var el=document.getElementById('banResult');el.classList.remove('hidden');if(d.success){el.innerHTML='<div class=""success"">'+d.message+'</div>'}else{el.innerHTML='<div class=""error"">'+d.error+'</div>'}btn.disabled=false;btn.textContent='🚫 执行封禁'}
async function unbanPlayer(){var target=document.getElementById('unbanTarget').value.trim();if(!target){document.getElementById('unbanResult').innerHTML='<div class=""error"">请输入SteamId</div>';document.getElementById('unbanResult').classList.remove('hidden');return}
var btn=document.getElementById('unbanBtn');btn.disabled=true;btn.textContent='处理中...';var r=await fetch('/api/ban/unban',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'targetId='+encodeURIComponent(target)});var d=await r.json();var el=document.getElementById('unbanResult');el.classList.remove('hidden');if(d.success){el.innerHTML='<div class=""success"">'+d.message+'</div>'}else{el.innerHTML='<div class=""error"">'+d.error+'</div>'}btn.disabled=false;btn.textContent='🔓 解除封禁'}
</script>", true, "管理员");
        }

        public static string ErrorPage(int code, string msg)
        {
            return Layout("错误",
@"<div style=""text-align:center;padding:100px 20px"">
<h1 style=""font-size:60px;-webkit-text-fill-color:#ff6b6b;background:linear-gradient(90deg,#ff6b6b,#ee5a24);-webkit-background-clip:text"">" + code + @"</h1>
<p style=""color:#aaa;font-size:16px"">" + HtmlEscape(msg) + @"</p>
<a href=""/"" style=""color:#88ddff;text-decoration:none;margin-top:20px;display:inline-block"">← 返回首页</a>
</div>", false);
        }

        // ========== 论坛页面 ==========

        public static string ForumPage(string userName, string token)
        {
            return Layout("论坛",
"<div class=\"fade-in\"><div class=\"card\"><h1>论坛</h1>" +
"<p style=\"color:#aaa;font-size:13px\">发帖需管理员审核</p>" +
"<div style=\"margin:15px 0\">" +
"<a href=\"/forum/new\" style=\"display:inline-block;padding:10px 20px;background:linear-gradient(90deg,#667eea,#764ba2);color:#fff;border-radius:8px;text-decoration:none;font-size:14px\">发布新帖</a>" +
"</div>" +
"<div id=\"forumList\"><div class=\"loading\">加载中...</div></div>" +
"</div></div>" +
"<script>" +
"(async function(){try{var d=await api('/api/forum/posts');if(d.success){var el=document.getElementById('forumList');if(d.posts.length===0){el.innerHTML='<p style=\"color:#666;text-align:center;padding:20px\">暂无帖子</p>';return}" +
"var html='<table><thead><tr><th>标题</th><th>分类</th><th>作者</th><th>时间</th><th>状态</th></tr></thead><tbody>';" +
"d.posts.forEach(function(p){var status=p.approved?'<span class=\"badge badge-online\">已审核</span>':'<span class=\"badge badge-offline\">待审核</span>';" +
"html+='<tr><td><a href=\"/forum/post?id='+p.id+'\" style=\"color:#88ddff;text-decoration:none\">'+p.title+'</a></td><td>'+p.category+'</td><td>'+p.authorName+'</td><td>'+p.createdAt+'</td><td>'+status+'</td></tr>'});" +
"html+='</tbody></table>';" +
"if(d.isAdmin)html+='<p style=\"margin-top:10px;font-size:12px;color:#888\">管理员可查看未审核帖子 <a href=\"/admin/forum\" style=\"color:#ff6\">审核管理</a></p>';" +
"el.innerHTML=html}}catch(e){document.getElementById('forumList').innerHTML='<div class=\"error\">加载失败</div>'}})()" +
"</script>", token != null, userName);
        }

        public static string ForumNewPage(string userName)
        {
            return Layout("发帖",
"<div class=\"fade-in\"><div class=\"card\" style=\"max-width:700px;margin:40px auto\">" +
"<h1>发布新帖</h1>" +
"<p style=\"color:#aaa;font-size:13px;margin-bottom:15px\">发帖后需管理员审核通过后可见</p>" +
"<label style=\"font-size:13px;color:#888\">标题</label>" +
"<input type=\"text\" id=\"postTitle\" placeholder=\"帖子标题\" maxlength=\"100\">" +
"<label style=\"font-size:13px;color:#888\">分类</label>" +
"<select id=\"postCategory\"><option value=\"general\">综合讨论</option><option value=\"suggestion\">建议反馈</option><option value=\"bug\">Bug报告</option><option value=\"other\">其他</option></select>" +
"<label style=\"font-size:13px;color:#888\">内容</label>" +
"<textarea id=\"postContent\" rows=\"8\" placeholder=\"在这里写你的内容...\" style=\"width:100%;padding:12px 15px;margin:8px 0;border:none;border-radius:8px;font-size:14px;outline:none;background:rgba(255,255,255,0.1);color:#fff;resize:vertical;font-family:inherit\"></textarea>" +
"<button onclick=\"submitPost()\" id=\"postBtn\">提交帖子</button>" +
"<div id=\"postResult\" class=\"hidden\"></div>" +
"</div></div>" +
"<script>" +
"async function submitPost(){var title=document.getElementById('postTitle').value.trim();var content=document.getElementById('postContent').value.trim();var cat=document.getElementById('postCategory').value;if(!title||!content){document.getElementById('postResult').innerHTML='<div class=\"error\">标题和内容不能为空</div>';document.getElementById('postResult').classList.remove('hidden');return}" +
"var btn=document.getElementById('postBtn');btn.disabled=true;btn.textContent='提交中...';var r=await fetch('/api/forum/post',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'title='+encodeURIComponent(title)+'&content='+encodeURIComponent(content)+'&category='+encodeURIComponent(cat)});var d=await r.json();if(d.success){document.getElementById('postResult').innerHTML='<div class=\"success\">'+d.message+'</div>';document.getElementById('postTitle').value='';document.getElementById('postContent').value=''}else{document.getElementById('postResult').innerHTML='<div class=\"error\">'+d.error+'</div>'}btn.disabled=false;btn.textContent='提交帖子'}" +
"</script>", true, userName);
        }

        public static string ForumPostPage(PostEntry post, bool canManage)
        {
            string id = post.Id;
            string title = HtmlEscape(post.Title);
            string author = HtmlEscape(post.AuthorName);
            string content = HtmlEscape(post.Content);
            string statusBadge = post.Approved
                ? "<span class=\"badge badge-online\">已审核</span>"
                : "<span class=\"badge badge-offline\">待审核</span>";
            string approveBtn = !post.Approved
                ? "<button onclick=\"approvePost('" + id + "','approve')\" style=\"background:#51cf66;width:auto;display:inline-block;margin-right:10px\">通过</button>"
                : "";
            string deleteBtn = "<button onclick=\"approvePost('" + id + "','delete')\" style=\"background:#ff4444;width:auto;display:inline-block\">删除</button>";
            string adminBtns = canManage ? "<div style=\"margin-top:15px\">" + approveBtn + deleteBtn + "</div>" : "";

            return Layout("帖子详情",
"<div class=\"fade-in\">" +
"<div class=\"card\">" +
"<div style=\"margin-bottom:10px\"><a href=\"/forum\" style=\"color:#88ddff;text-decoration:none;font-size:13px\">← 返回论坛</a></div>" +
"<h1>" + title + "</h1>" +
"<div style=\"font-size:12px;color:#888;margin-bottom:15px\">" +
"<span>" + author + "</span> · <span>" + post.CreatedAt + "</span> · <span>" + post.Category + "</span> · <span>👁 " + post.Views + "</span>" +
statusBadge +
"</div>" +
"<div style=\"line-height:1.8;font-size:14px;color:#ddd;white-space:pre-wrap\">" + content + "</div>" +
adminBtns +
"</div></div>" +
"<script>" +
"async function approvePost(id,action){if(action==='delete'&&!confirm('确认删除？'))return;" +
"var r=await fetch('/api/forum/post/'+(action==='delete'?'delete':'approve'),{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'postId='+id+'&action='+(action==='approve'?'approve':'reject')});var d=await r.json();if(d.success){location.reload()}else{alert(d.error)}}" +
"</script>", true, "用户");
        }

        public static string AdminForumPage(string token)
        {
            return Layout("论坛审核",
"<div class=\"fade-in\"><div class=\"card\">" +
"<h1>帖子审核管理</h1>" +
"<p style=\"color:#aaa;font-size:13px\">查看所有待审核和已审核的帖子</p>" +
"<div id=\"forumAdminList\"><div class=\"loading\">加载中...</div></div>" +
"</div></div>" +
"<script>" +
"(async function(){try{var d=await api('/api/forum/posts');if(d.success){var el=document.getElementById('forumAdminList');" +
"var html='<table><thead><tr><th>标题</th><th>作者</th><th>时间</th><th>状态</th><th>操作</th></tr></thead><tbody>';" +
"d.posts.forEach(function(p){var status=p.approved?'<span class=\"badge badge-online\">已通过</span>':'<span class=\"badge badge-offline\">待审核</span>';" +
"var btns='';" +
"if(!p.approved)btns+='<button onclick=\"approve(&quot;'+p.id+'&quot;,&quot;approve&quot;)\" style=\"background:#51cf66;width:auto;padding:5px 10px;margin:2px\">通过</button>';" +
"btns+='<button onclick=\"approve(&quot;'+p.id+'&quot;,&quot;delete&quot;)\" style=\"background:#ff4444;width:auto;padding:5px 10px;margin:2px\">删除</button>';" +
"html+='<tr><td><a href=\"/forum/post?id='+p.id+'\" style=\"color:#88ddff;text-decoration:none\">'+p.title+'</a></td><td>'+p.authorName+'</td><td>'+p.createdAt+'</td><td>'+status+'</td><td>'+btns+'</td></tr>'});" +
"html+='</tbody></table>';el.innerHTML=html}}catch(e){document.getElementById('forumAdminList').innerHTML='<div class=\"error\">加载失败</div>'}})();" +
"async function approve(id,action){if(action==='delete'&&!confirm('确认删除？'))return;" +
"var r=await fetch('/api/forum/post/'+(action==='delete'?'delete':'approve'),{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'postId='+id+'&action='+(action==='approve'?'approve':'reject')});var d=await r.json();if(d.success){location.reload()}else{alert(d.error)}}" +
"</script>", true, "管理员");
        }

        // ========== 举报页面 ==========

        public static string ReportPage(string userName, string token)
        {
            return Layout("举报玩家",
@"<div class=""fade-in"">
<div class=""card"" style=""max-width:700px;margin:40px auto"">
<h1>🚨 举报玩家</h1>
<p style=""color:#aaa;font-size:13px;margin-bottom:15px"">举报违规玩家，<strong style=""color:#ff6b6b"">必须提供图片或视频证据</strong></p>

<label style=""font-size:13px;color:#888"">被举报玩家ID或昵称 *</label>
<input type=""text"" id=""repTarget"" placeholder=""输入被举报人的SteamId或游戏昵称"">

<label style=""font-size:13px;color:#888"">违规原因 *</label>
<select id=""repReason"">
<option value=""外挂/作弊"">外挂/作弊</option>
<option value=""辱骂/人身攻击"">辱骂/人身攻击</option>
<option value=""恶意组伤"">恶意组伤</option>
<option value=""恶意卡BUG"">恶意卡BUG</option>
<option value=""刷屏/广告"">刷屏/广告</option>
<option value=""其他"">其他</option>
</select>

<label style=""font-size:13px;color:#888"">详细描述</label>
<textarea id=""repDesc"" rows=""4"" placeholder=""描述违规经过..."" style=""width:100%;padding:12px 15px;margin:8px 0;border:none;border-radius:8px;font-size:14px;outline:none;background:rgba(255,255,255,0.1);color:#fff;resize:vertical;font-family:inherit""></textarea>

<label style=""font-size:13px;color:#888"">📎 上传证据（图片/视频）*</label>
<div style=""border:2px dashed rgba(255,255,255,0.2);border-radius:8px;padding:20px;text-align:center;margin:8px 0;cursor:pointer"" onclick=""document.getElementById('evidenceFile').click()"">
<p id=""evidencePlaceholder"" style=""color:#888;font-size:13px"">点击选择文件</p>
<input type=""file"" id=""evidenceFile"" accept=""image/*,video/*"" style=""display:none"" onchange=""handleFile(this)"">
</div>
<div id=""evidencePreview"" class=""hidden""></div>

<label style=""font-size:13px;color:#888"">或者提供证据链接（百度网盘/YouTube等）</label>
<input type=""url"" id=""evidenceUrl"" placeholder=""https://..."">

<button onclick=""submitReport()"" id=""reportBtn"" style=""margin-top:10px;background:linear-gradient(90deg,#ff6b6b,#ee5a24)"">📤 提交举报</button>
<div id=""reportResult"" class=""hidden""></div>
</div>
</div>
<script>
var selectedFile=null;
function handleFile(input){if(input.files&&input.files[0]){selectedFile=input.files[0];document.getElementById('evidencePlaceholder').textContent=selectedFile.name+' ('+Math.round(selectedFile.size/1024)+'KB)';
var reader=new FileReader();reader.onload=function(e){var preview=document.getElementById('evidencePreview');
if(selectedFile.type.startsWith('video/')){preview.innerHTML='<video controls style=""max-width:100%;max-height:200px;border-radius:8px""><source src=""'+e.target.result+'""></video>'}else{preview.innerHTML='<img src=""'+e.target.result+'" style=""max-width:100%;max-height:200px;border-radius:8px"">'}
preview.classList.remove('hidden')};reader.readAsDataURL(input.files[0])}}
async function submitReport(){var target=document.getElementById('repTarget').value.trim();var reason=document.getElementById('repReason').value;var desc=document.getElementById('repDesc').value.trim();var url=document.getElementById('evidenceUrl').value.trim();if(!target){document.getElementById('reportResult').innerHTML='<div class=""error"">请输入被举报玩家</div>';document.getElementById('reportResult').classList.remove('hidden');return}
if(!selectedFile&&!url){document.getElementById('reportResult').innerHTML='<div class=""error"">必须提供图片或视频证据</div>';document.getElementById('reportResult').classList.remove('hidden');return}
var btn=document.getElementById('reportBtn');btn.disabled=true;btn.textContent='提交中...';
var formData=new FormData();formData.append('targetId',target);formData.append('reason',reason);formData.append('description',desc);formData.append('evidenceUrl',url);
if(selectedFile)formData.append('evidence',selectedFile);
var r=await fetch('/api/report/create',{method:'POST',headers:{'Authorization':'Bearer '+getCookie('session')},body:formData});var d=await r.json();
if(d.success){document.getElementById('reportResult').innerHTML='<div class=""success"">'+d.message+'</div>';document.getElementById('repTarget').value='';document.getElementById('repDesc').value='';document.getElementById('evidenceUrl').value='';selectedFile=null;document.getElementById('evidencePlaceholder').textContent='点击选择文件';document.getElementById('evidencePreview').classList.add('hidden')}else{document.getElementById('reportResult').innerHTML='<div class=""error"">'+d.error+'</div>'}
btn.disabled=false;btn.textContent='📤 提交举报'}
</script>", token != null, userName);
        }

        public static string AdminReportsPage(string token)
        {
            return Layout("举报管理",
"<div class=\"fade-in\"><div class=\"card\">" +
"<h1>举报管理</h1>" +
"<div style=\"margin:10px 0;display:flex;gap:10px\">" +
"<button onclick=\"loadReports('all')\" id=\"filterAll\" style=\"flex:1;background:rgba(102,126,234,0.3);border:2px solid rgba(102,126,234,0.5)\">全部</button>" +
"<button onclick=\"loadReports('pending')\" id=\"filterPending\" style=\"flex:1;background:rgba(255,255,255,0.05);border:2px solid rgba(255,255,255,0.1)\">待处理</button>" +
"</div>" +
"<div id=\"reportList\"><div class=\"loading\">加载中...</div></div>" +
"</div></div>" +
"<script>" +
"var currentFilter='all';" +
"async function loadReports(filter){currentFilter=filter;document.getElementById('reportList').innerHTML='<div class=\"loading\">加载中...</div>';" +
"document.getElementById('filterAll').style.background=filter==='all'?'rgba(102,126,234,0.3)':'rgba(255,255,255,0.05)';" +
"document.getElementById('filterAll').style.border=filter==='all'?'2px solid rgba(102,126,234,0.5)':'2px solid rgba(255,255,255,0.1)';" +
"document.getElementById('filterPending').style.background=filter==='pending'?'rgba(102,126,234,0.3)':'rgba(255,255,255,0.05)';" +
"document.getElementById('filterPending').style.border=filter==='pending'?'2px solid rgba(102,126,234,0.5)':'2px solid rgba(255,255,255,0.1)';" +
"try{var d=await api('/api/report/list?filter='+filter);if(d.success){var el=document.getElementById('reportList');" +
"var html='<table><thead><tr><th>举报人</th><th>被举报人</th><th>原因</th><th>时间</th><th>状态</th><th>操作</th></tr></thead><tbody>';" +
"d.reports.forEach(function(r){var status=r.status==='pending'?'<span class=\"badge badge-offline\">待处理</span>':r.status==='approved'?'<span class=\"badge badge-online\">已封禁</span>':'<span class=\"badge\">已驳回</span>';" +
"var btns='<a href=\"/report?id='+r.id+'\" style=\"color:#88ddff;font-size:12px\">详情</a>';" +
"if(r.status==='pending'){btns+='<button onclick=\"processReport('+r.id+',&quot;approved&quot;)\" style=\"background:#51cf66;width:auto;padding:4px 8px;font-size:12px;margin:2px\">封禁</button>';" +
"btns+='<button onclick=\"processReport('+r.id+',&quot;rejected&quot;)\" style=\"background:#888;width:auto;padding:4px 8px;font-size:12px;margin:2px\">驳回</button>'}" +
"html+='<tr><td>'+r.reporterName+'</td><td>'+r.targetName+'</td><td>'+r.reason+'</td><td>'+r.createdAt+'</td><td>'+status+'</td><td>'+btns+'</td></tr>'});" +
"html+='</tbody></table>';el.innerHTML=html}}catch(e){document.getElementById('reportList').innerHTML='<div class=\"error\">加载失败</div>'}}loadReports('pending');" +
"async function processReport(id,action){if(action==='approved'&&!confirm('确认封禁该玩家？'))return;" +
"var r=await fetch('/api/report/process',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded','Authorization':'Bearer '+getCookie('session')},body:'reportId='+id+'&action='+action});var d=await r.json();if(d.success){loadReports(currentFilter)}else{alert(d.error)}}" +
"</script>", true, "管理员");
        }

        private static string HtmlEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\"", "&quot;").Replace("'", "&#39;");
        }
    }
}
