using System;
using Exiled.API.Features;
using HarmonyLib;

namespace SCPplaus
{
    /// <summary>
    /// 修复 SCP-127 掉落物引发的无限异常刷屏：
    /// 地图柜子生成 SCP-127 时，EXILED 的 FirearmPickup.InitializeProperties 会读取 BaseDamage，
    /// 而 Scp127Hitscan.get_BaseDamage → Scp127TierManagerModule.GetStats(hub) 在 hub 为 null
    /// （掉落物没有关联玩家）时抛 NullReferenceException，导致 Locker.Update() 每帧重试填充柜子，
    /// 从而产生每秒数十次异常 + 日志暴涨 + CPU 空耗。
    /// 这里用 Finalizer 捕获异常并返回默认伤害，让 Pickup 正常创建、柜子填充完成。
    /// </summary>
    internal static class Scp127FixPatch
    {
        private const string HarmonyId = "zeropl.scpplaus.scp127.fix";
        private static Harmony _harmony;

        public static void Enable()
        {
            if (_harmony != null) return;
            try
            {
                var target = AccessTools.Method(
                    "InventorySystem.Items.Firearms.Modules.Scp127.Scp127Hitscan:get_BaseDamage");
                if (target == null)
                {
                    Log.Warn("[SCPplaus] 未找到 Scp127Hitscan.get_BaseDamage，SCP-127 修复未启用");
                    return;
                }

                var finalizer = new HarmonyMethod(AccessTools.Method(typeof(Scp127FixPatch), nameof(FixBaseDamage)));
                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(target, finalizer: finalizer);
                Log.Info("[SCPplaus] SCP-127 掉落物异常刷屏修复已启用");
            }
            catch (Exception ex)
            {
                Log.Error($"[SCPplaus] SCP-127 修复启用失败: {ex.Message}");
                _harmony = null;
            }
        }

        public static void Disable()
        {
            if (_harmony == null) return;
            try { _harmony.UnpatchAll(HarmonyId); }
            catch (Exception ex) { Log.Error($"[SCPplaus] SCP-127 修复卸载失败: {ex.Message}"); }
            _harmony = null;
        }

        /// <summary>Finalizer：吞掉异常并返回默认伤害值，阻止异常传播导致柜子无限重试</summary>
        private static Exception FixBaseDamage(Exception __exception, ref float __result)
        {
            if (__exception != null)
            {
                __result = 30f;
                return null;
            }
            return null;
        }
    }
}
