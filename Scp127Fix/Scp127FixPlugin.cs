using System;
using Exiled.API.Features;

namespace Scp127Fix
{
    public class Scp127FixPlugin : Plugin<Scp127FixConfig>
    {
        public override string Name => "Scp127Fix";
        public override string Author => "Developer";
        public override string Prefix => "scp127fix";
        public override Version Version => new Version(1, 0, 0);

        public override void OnEnabled()
        {
            if (Config.FixEnabled)
                Scp127Patch.Enable();
            Log.Info($"{Name} 加载完成 — SCP-127 掉落物异常修复: {(Config.FixEnabled ? "开" : "关")}");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Scp127Patch.Disable();
            base.OnDisabled();
        }
    }
}
