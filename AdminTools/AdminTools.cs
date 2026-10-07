using System;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace AdminTools
{
    public class AdminTools : Plugin<AdminToolsConfig>
    {
        public static AdminTools Instance { get; private set; }

        public AdminManager Manager { get; private set; }

        public override string Name => "AdminTools";
        public override string Author => "Developer";
        public override string Prefix => "admintools";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        public override void OnEnabled()
        {
            Instance = this;
            Manager = new AdminManager(this);

            // 将已记录的管理员权限应用到在线玩家
            Exiled.Events.Handlers.Player.Verified += OnPlayerVerified;

            base.OnEnabled();
            Log.Info($"[AdminTools] 已加载，当前 {Manager.GetAllAdmins().Count} 位管理员");
        }

        public override void OnDisabled()
        {
            Exiled.Events.Handlers.Player.Verified -= OnPlayerVerified;
            Instance = null;
            base.OnDisabled();
        }

        private void OnPlayerVerified(Exiled.Events.EventArgs.Player.VerifiedEventArgs ev)
        {
            try
            {
                if (ev.Player == null) return;
                int level = Manager.GetAdminLevel(ev.Player.UserId);
                if (level >= 3)
                    Manager.ApplyPermissions(ev.Player, level);
            }
            catch (Exception ex)
            {
                Log.Error($"[AdminTools] 应用玩家权限失败: {ex.Message}");
            }
        }
    }
}
