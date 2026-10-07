using System;
using Exiled.API.Features;
using HarmonyLib;
using InventorySystem.Items;
using MapGeneration.Distributors;

namespace Scp127Fix
{
    /// <summary>
    /// 修复 SCP-127 引发的服务器卡顿（每帧异常刷屏）：
    ///
    /// 现象：地图柜子尝试生成 SCP-127（ItemType.GunSCP127）→ Exiled 包装掉落物时
    /// FirearmPickup.InitializeProperties 读取 BaseDamage / BaseBulletInaccuracy 等属性 →
    /// Scp127Hitscan.get_* → Scp127TierManagerModule.GetStats(hub) 在 hub 为 null（掉落物没有关联玩家）时
    /// 抛 NullReferenceException → Locker 判定柜子未填充成功 → 在 Locker.Update() 里每帧重试 →
    /// 每秒数十次异常堆栈、日志暴涨（数 MB/分钟）、CPU 空耗，玩家体感严重卡顿。
    ///
    /// 修复分两层：
    /// 1) 治本：Prefix 拦截 LockerChamber.SpawnItem，当 id 为 GunSCP127 时跳过生成
    ///    （掉落物形态的 SCP-127 在当前 EXILED 版本下无法正常创建，跳过等于维持"地图上本来也没有"的现状，
    ///     但消除了每帧重试）；
    /// 2) 兜底：给 Scp127Hitscan.get_BaseDamage / get_BaseBulletInaccuracy 挂 Finalizer，
    ///    万一还有其它途径创建 SCP-127 掉落物，也吞掉异常返回默认值，不再中断柜子填充流程。
    /// </summary>
    internal static class Scp127Patch
    {
        private const string HarmonyId = "zeropl.scp127fix";
        private static Harmony _harmony;

        public static void Enable()
        {
            if (_harmony != null) return;
            try
            {
                _harmony = new Harmony(HarmonyId);

                // 1) 治本：阻止柜子生成 SCP-127
                var spawnItem = AccessTools.Method(typeof(LockerChamber), "SpawnItem");
                if (spawnItem != null)
                {
                    _harmony.Patch(spawnItem,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(Scp127Patch), nameof(SkipScp127Prefix))));
                    Log.Info("[Scp127Fix] 已拦截 LockerChamber.SpawnItem，阻止生成 SCP-127");
                }
                else
                {
                    Log.Warn("[Scp127Fix] 未找到 LockerChamber.SpawnItem，跳过拦截");
                }

                // 2) 兜底：SCP-127 getter 异常吞掉
                PatchFinalizer("InventorySystem.Items.Firearms.Modules.Scp127.Scp127Hitscan:get_BaseDamage");
                PatchFinalizer("InventorySystem.Items.Firearms.Modules.Scp127.Scp127Hitscan:get_BaseBulletInaccuracy");
            }
            catch (Exception ex)
            {
                Log.Error($"[Scp127Fix] 修复启用失败: {ex.Message}");
                _harmony = null;
            }
        }

        public static void Disable()
        {
            if (_harmony == null) return;
            try { _harmony.UnpatchAll(HarmonyId); }
            catch (Exception ex) { Log.Error($"[Scp127Fix] 修复卸载失败: {ex.Message}"); }
            _harmony = null;
        }

        private static void PatchFinalizer(string methodSpec)
        {
            var m = AccessTools.Method(methodSpec);
            if (m == null)
            {
                Log.Warn($"[Scp127Fix] 未找到 {methodSpec}");
                return;
            }
            _harmony.Patch(m,
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(Scp127Patch), nameof(FixValue))));
        }

        /// <summary>Prefix：返回 false 跳过原方法，从而不生成 SCP-127</summary>
        private static bool SkipScp127Prefix(ItemType id)
        {
            return id != ItemType.GunSCP127;
        }

        /// <summary>Finalizer：吞掉异常并返回默认值，阻止异常传播到 Locker 填充流程</summary>
        private static Exception FixValue(Exception __exception, ref float __result)
        {
            if (__exception != null)
            {
                __result = 0f;
                return null;
            }
            return null;
        }
    }
}
