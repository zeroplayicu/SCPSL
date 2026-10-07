using System;
using CommandSystem;
using Exiled.API.Features;
using RemoteAdmin;

namespace FactionPlugin.Commands
{
    /// <summary>
    /// 统一特殊刷新命令（管理面板 RA 使用）：
    ///   sx goc            强制刷新 GOC 攻击小组（忽略对局时间与"每回合一次"限制）
    ///   sx scp999 [玩家]   刷新 SCP-999（不填玩家则随机选一名观战者）
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class SxCommand : ICommand
    {
        public string Command => "sx";
        public string[] Aliases => new[] { "sxrefresh" };
        public string Description => "特殊刷新: sx goc（强制刷新GOC攻击小组）| sx scp999 [玩家]（刷新SCP-999）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "你没有权限执行此命令";
                return false;
            }

            // 特殊角色只在 7779 实例启用（用户要求 2026-10-02）
            if (!FactionPlugin.SpecialRolesEnabled)
            {
                response = "该服务器实例（7778）未启用特殊角色功能";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "用法: sx goc | sx scp999 [玩家ID/名称]";
                return true;
            }

            string sub = arguments.At(0).ToLowerInvariant();

            if (sub == "nu7" || sub == "nu7a")
            {
                // sx nu7 [兵种] —— ylb=医疗兵 sb=士兵 jj=狙击手 zhg=指挥官燕双鹰 qjs=机枪手 xf=先锋
                if (arguments.Count > 1)
                {
                    string roleName = arguments.At(1).ToLowerInvariant();
                    Nu7RoleType type;
                    switch (roleName)
                    {
                        case "ylb": case "yiliaobing": type = Nu7RoleType.Medic; break;
                        case "sb": case "shibing": type = Nu7RoleType.Soldier; break;
                        case "jj": case "jujishou": type = Nu7RoleType.Sniper; break;
                        case "zhg": case "zhihuiguan": type = Nu7RoleType.Commander; break;
                        case "qjs": case "jiqiangshou": type = Nu7RoleType.Gunner; break;
                        case "xf": case "xianfeng": type = Nu7RoleType.Vanguard; break;
                        default:
                            response = "未知兵种。可用: ylb(医疗兵) sb(士兵) jj(狙击手) zhg(指挥官) qjs(机枪手) xf(先锋)";
                            return false;
                    }
                    bool okSingle = Nu7Manager.ForceSpawnSingle(type, out string singleResp);
                    response = singleResp;
                    return okSingle;
                }

                bool ok = Nu7Manager.ForceSpawn(out string nu7Resp);
                response = nu7Resp;
                return ok;
            }

            if (sub == "goc")
            {
                // sx goc [兵种] —— sb=士兵 zz=重装 zdzj=战斗专家 ylb=医疗兵 zhg=指挥官
                if (arguments.Count > 1)
                {
                    string roleName = arguments.At(1).ToLowerInvariant();
                    GocRoleType type;
                    switch (roleName)
                    {
                        case "sb": case "shibing": type = GocRoleType.Soldier; break;
                        case "xf": case "xianfeng": type = GocRoleType.Vanguard; break;
                        case "tz": case "tezhan": type = GocRoleType.SpecialOps; break;
                        case "zz": case "zhongzhuang": type = GocRoleType.Heavy; break;
                        case "zdzj": case "zhandouzhuangjia": type = GocRoleType.Breacher; break;
                        case "ylb": case "yiliaobing": type = GocRoleType.Medic; break;
                        case "zhg": case "zhihuiguan": type = GocRoleType.Commander; break;
                        case "qss": case "qishushi": type = GocRoleType.Thaumaturge; break;
                        default:
                            response = "未知兵种。可用: sb(士兵) zz(重装) zdzj(战斗专家) ylb(医疗兵) zhg(指挥官) qss(奇术师)";
                            return false;
                    }

                    bool okSingle = GocManager.ForceSpawnSingle(type, out string singleResponse);
                    response = singleResponse;
                    return okSingle;
                }

                bool ok = GocManager.ForceSpawn();
                response = ok ? "已强制刷新 GOC 攻击小组" : "刷新失败：阴间没有可复活的玩家";
                return true;
            }

            if (sub == "181")
            {
                Player target181 = null;
                if (arguments.Count > 1)
                {
                    if (int.TryParse(arguments.At(1), out int pid181))
                        target181 = Player.Get(pid181);
                    if (target181 == null)
                        target181 = Player.Get(arguments.At(1));
                }

                if (target181 == null)
                {
                    foreach (var p in Player.List)
                    {
                        if (p != null && p.IsConnected && !p.IsAlive && !Scp999Manager.IsScp999(p) && !GocManager.IsGoc(p))
                        {
                            target181 = p;
                            break;
                        }
                    }
                }

                if (target181 == null || !target181.IsConnected)
                {
                    response = "没有可用的玩家来变身 SCP-181";
                    return false;
                }

                if (GocManager.IsGoc(target181) || Scp999Manager.IsScp999(target181))
                {
                    response = $"{target181.Nickname} 已是特殊角色（GOC/999），不能重复变身";
                    return false;
                }

                Scp181Manager.Spawn(target181);
                response = $"{target181.Nickname} 已变身为 SCP-181 幸运儿";
                return true;
            }

            if (sub == "scp999" || sub == "999")
            {
                Player target = null;
                if (arguments.Count > 1)
                {
                    if (int.TryParse(arguments.At(1), out int pid))
                        target = Player.Get(pid);
                    if (target == null)
                        target = Player.Get(arguments.At(1));
                }

                if (target == null)
                {
                    // 随机选一名观战者（排除已变身特殊角色的玩家）
                    foreach (var p in Player.List)
                    {
                        if (p != null && p.IsConnected && !p.IsAlive && !Scp999Manager.IsScp999(p) && !GocManager.IsGoc(p) && !Scp181Manager.Is181(p))
                        {
                            target = p;
                            break;
                        }
                    }
                }

                if (target == null || !target.IsConnected)
                {
                    response = "没有可用的玩家来召唤 SCP-999";
                    return false;
                }

                if (Scp999Manager.IsScp999(target) || GocManager.IsGoc(target))
                {
                    response = $"{target.Nickname} 已是特殊角色（GOC/999），不能重复变身";
                    return false;
                }

                Scp999Manager.SpawnScp999(target);
                response = $"{target.Nickname} 已变身为 SCP-999";
                return true;
            }

            response = "未知子命令。可用: sx goc | sx scp999 [玩家]";
            return false;
        }
    }
}
