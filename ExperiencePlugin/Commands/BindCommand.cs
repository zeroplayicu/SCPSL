using System;
using CommandSystem;
using Exiled.API.Features;

namespace ExperiencePlugin.Commands
{
    /// <summary>
    /// .register / .bind 指令
    /// 第一步: .register - 注册账号，获取6位绑定码
    /// 第二步: .bind <6位码> - 绑定该码到当前账号
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BindCommand : ICommand
    {
        public string Command => "bind";
        public string[] Aliases => new[] { "register" };
        public string Description => "注册/绑定游戏账号到Web面板。.register 注册获取码，.bind <码> 完成绑定";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                var player = Player.Get(sender);
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                string userId = player.UserId;
                var bindMgr = ExperiencePlugin.Instance.BindManager;

                // ===== 第一步：.register（无参数）= 注册 =====
                if (arguments.Count == 0)
                {
                    // 已注册但未绑定 → 提示去绑定
                    if (bindMgr.HasRegistered(userId) && !bindMgr.IsBound(userId))
                    {
                        string code = bindMgr.GetPendingCode(userId);
                        response = "<color=#44FF88>已注册！请完成绑定</color>\n" +
                                   "请在控制台输入: <color=yellow>.bind " + code + "</color>\n" +
                                   "将此绑定码绑定到你的账号";
                        return true;
                    }

                    // 已绑定 → 显示绑定码
                    if (bindMgr.IsBound(userId))
                    {
                        string code = bindMgr.GetCodeBySteamId(userId);
                        response = "<color=#44FF88>已绑定！</color>\n" +
                                   "你的绑定码: <color=yellow>" + code + "</color>\n" +
                                   "在网页端使用此码登录";
                        return true;
                    }

                    // 未注册 → 注册并生成码
                    string newCode = bindMgr.GenerateBindCode(userId);
                    response = "<color=#44FF88>注册成功！</color>\n" +
                               "你的绑定码: <color=yellow><size=36>" + newCode + "</size></color>\n" +
                               "<color=#FFD700>请继续输入: .bind " + newCode + "</color>\n" +
                               "完成绑定后方可在Web面板登录";
                    return true;
                }

                // ===== 第二步：.bind <6位码> =====
                string inputCode = arguments.At(0);
                if (inputCode.Length != 6 || !int.TryParse(inputCode, out _))
                {
                    response = "绑定码必须是6位数字！\n第一步: .register\n第二步: .bind <6位码>";
                    return false;
                }

                // 已绑定 → 提示
                if (bindMgr.IsBound(userId))
                {
                    response = "你已经绑定过了！绑定码: <color=yellow>" + bindMgr.GetCodeBySteamId(userId) + "</color>";
                    return true;
                }

                // 检查该绑定码是否已被其他玩家绑定
                string existingSteamId = bindMgr.GetSteamIdByCode(inputCode);
                if (existingSteamId != null)
                {
                    if (existingSteamId == userId)
                    {
                        response = "你已绑定，绑定码: <color=yellow>" + inputCode + "</color>";
                        return true;
                    }
                    response = "该绑定码已被其他玩家使用！";
                    return false;
                }

                // 执行绑定
                bindMgr.ForceBind(userId, inputCode);

                response = "<color=#44FF88>绑定成功！</color>\n" +
                           "绑定码: <color=yellow><size=36>" + inputCode + "</size></color>\n" +
                           "现在可以使用此绑定码在 Web 面板登录";
                Log.Info("[Bind] " + player.Nickname + " 绑定成功，绑定码: " + inputCode);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[Bind] 错误: " + ex.Message);
                response = "绑定失败，请联系管理员";
                return false;
            }
        }
    }
}
