using System;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp3114;

namespace SCPplaus
{
    /// <summary>
    /// SCP-3114 掐喉无冷却补丁：
    /// postfix 清零 Scp3114Strangle 中所有冷却相关 float 字段（Cooldown / _postReleaseCooldown 等），
    /// 使掐喉结束后立即可再次使用。字段名采用模糊匹配，兼容游戏版本更新导致的改名。
    /// </summary>
    internal static class Scp3114StranglePatch
    {
        private const string HarmonyId = "zeropl.scpplaus.scp3114.nocooldown";
        private static Harmony _harmony;

        public static void Enable()
        {
            if (_harmony != null) return;
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(StrangleCooldownPatch).Assembly);
            Log.Info("[SCPplaus] SCP-3114 掐喉无冷却补丁已启用");
        }

        public static void Disable()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
            Log.Info("[SCPplaus] SCP-3114 掐喉无冷却补丁已卸载");
        }

        [HarmonyPatch(typeof(Scp3114Strangle), "ServerProcessCmd")]
        internal static class StrangleCooldownPatch
        {
            // 静态缓存：Scp3114Strangle 中名字含 "cooldown" 的 float 实例字段
            private static readonly FieldInfo[] CooldownFields;

            static StrangleCooldownPatch()
            {
                try
                {
                    CooldownFields = typeof(Scp3114Strangle)
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(f => f.FieldType == typeof(float) &&
                                    f.Name.IndexOf("cooldown", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToArray();
                    Log.Info($"[SCPplaus] 3114掐喉冷却字段定位: {string.Join(", ", CooldownFields.Select(f => f.Name))}");
                }
                catch (Exception ex)
                {
                    CooldownFields = Array.Empty<FieldInfo>();
                    Log.Error($"[SCPplaus] 3114冷却字段初始化失败: {ex.Message}");
                }
            }

            private static void Postfix(object __instance)
            {
                try
                {
                    foreach (var f in CooldownFields)
                        f.SetValue(__instance, 0f);
                }
                catch { /* 单帧清零失败不影响下一帧 */ }
            }
        }
    }
}
