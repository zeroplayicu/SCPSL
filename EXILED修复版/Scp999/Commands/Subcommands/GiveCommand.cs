using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.CustomRoles.API.Features;
using Exiled.Permissions.Extensions;
using Scp999.Features;

namespace Scp999.Commands;
public class GiveCommand : ICommand
{
    public string Command => "give";
    public string Description => "Give a custom role SCP-999 for player";
    public string[] Aliases => [];

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        // 本轮修复: ParentCommand 分发子命令时不经过 ExecuteParent 的权限校验，
        // 原实现任何能打开 RemoteAdmin 面板的人都可以绕过 .scp999 权限发放角色。
        // 与 MainCommand.ExecuteParent 的校验保持一致。
        if (!((CommandSender)sender).CheckPermission(".scp999"))
        {
            response = "You do not have permission to use this command!";
            return false;
        }

        if (arguments.Count != 1)
        {
            response = $"Specify the player id to the command: scp999 give [id]";
            return false;
        }
        
        Player player = Player.Get(arguments.At(0));
        if (player == null)
        {
            response = $"Player not found: {arguments.At(0)}";
            return false;
        }
        
        var scp999Role = CustomRole.Get(typeof(Scp999Role));
        if (scp999Role == null)
        {
            response = "Custom role SCP-999 role not found or not registered";
            return false;
        }

        if (scp999Role.Check(player))
        {
            response = "The player already have the custom role SCP-999";
            return false;
        }
        
        scp999Role.AddRole(player);
        response = $"<color=green>Custom role SCP-999 granted for {player.Nickname}</color>";
        return true;
    }
}