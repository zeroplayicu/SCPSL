using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FactionPlugin
{
    /// <summary>
    /// SCP-999 技能系统。
    /// 技能1：附近人类获得肾上腺素（持续15秒，冷却40秒）。
    /// 技能2：给附近所有单位每秒回复(SCP 50/人类 25)持续10秒，冷却30秒。
    /// </summary>
    public static class Scp999SkillManager
    {
        /// <summary>每个玩家的技能冷却时间记录（键=UserId，值=技能编号到下次可用时间）</summary>
        private static readonly Dictionary<string, Dictionary<int, DateTime>> Cooldowns =
            new Dictionary<string, Dictionary<int, DateTime>>();

        private static readonly HashSet<string> ActiveSkill2 = new HashSet<string>();

        private static FactionConfig Config => FactionPlugin.Instance.Config;

        /// <summary>尝试释放技能1(肾上腺素)。返回是否成功释放。</summary>
        public static bool TryCastSkill1(Player player)
        {
            if (player == null || !Scp999Manager.IsScp999(player)) return false;

            if (IsOnCooldown(player, 1))
            {
                player.ShowHint($"<color=#FF4444>[技能1] 冷却中，剩余 {GetCooldownRemaining(player, 1):F0} 秒</color>", 1.5f);
                return false;
            }

            float radius = Config.Scp999SkillRadius;
            int count = 0;
            foreach (var target in Player.List)
            {
                if (target == null || !target.IsAlive || target == player) continue;
                if (target.IsScp) continue; // 只对附近人类生效

                if (Vector3.Distance(player.Position, target.Position) <= radius)
                {
                    target.EnableEffect(EffectType.Invigorated, Config.Scp999Skill1Duration);
                    count++;
                }
            }

            SetCooldown(player, 1, Config.Scp999Skill1Cooldown);
            player.ShowHint($"<color=#00FF00>[技能1] 肾上腺素释放成功！影响 {count} 名人类，持续 {Config.Scp999Skill1Duration:0} 秒</color>", 3f);
            Log.Info($"[SCP-999] {player.Nickname} 释放技能1(肾上腺素)，影响 {count} 人");
            return true;
        }

        /// <summary>尝试释放技能2(范围回复)。返回是否成功释放。</summary>
        public static bool TryCastSkill2(Player player)
        {
            if (player == null || !Scp999Manager.IsScp999(player)) return false;

            if (IsOnCooldown(player, 2))
            {
                player.ShowHint($"<color=#FF4444>[技能2] 冷却中，剩余 {GetCooldownRemaining(player, 2):F0} 秒</color>", 1.5f);
                return false;
            }

            if (ActiveSkill2.Contains(player.UserId)) return false;

            SetCooldown(player, 2, Config.Scp999Skill2Cooldown);
            ActiveSkill2.Add(player.UserId);

            player.ShowHint($"<color=#00FF00>[技能2] 范围回复开启！持续 {Config.Scp999Skill2Duration:0} 秒(SCP+50/人类+25)</color>", 3f);
            Log.Info($"[SCP-999] {player.Nickname} 释放技能2(范围回复)");

            // 持续10秒，每秒回复一次
            Timing.RunCoroutine(Skill2Routine(player));
            return true;
        }

        private static IEnumerator<float> Skill2Routine(Player player)
        {
            float duration = Config.Scp999Skill2Duration;
            float radius = Config.Scp999SkillRadius;
            int ticks = (int)Math.Ceiling(duration);

            for (int i = 0; i < ticks; i++)
            {
                if (player == null || !player.IsConnected || !player.IsAlive)
                    break;

                foreach (var target in Player.List)
                {
                    if (target == null || !target.IsAlive || target == player) continue;
                    if (Vector3.Distance(player.Position, target.Position) > radius) continue;

                    if (target.IsScp)
                    {
                        float newHp = Math.Min(target.Health + Config.Scp999Skill2ScpHeal, target.MaxHealth);
                        target.Health = newHp;
                    }
                    else
                    {
                        float newHp = Math.Min(target.Health + Config.Scp999Skill2HumanHeal, target.MaxHealth);
                        target.Health = newHp;
                    }
                }

                yield return Timing.WaitForOneFrame;
                yield return Timing.WaitForSeconds(1f);
            }

            ActiveSkill2.Remove(player.UserId);
        }

        // ==================== 冷却管理 ====================

        private static bool IsOnCooldown(Player player, int skillId)
        {
            if (!Cooldowns.TryGetValue(player.UserId, out var map)) return false;
            if (!map.TryGetValue(skillId, out var time)) return false;
            return DateTime.UtcNow < time;
        }

        /// <summary>获取玩家指定技能的冷却剩余秒数（0表示就绪），供SSS冷却倒计时显示</summary>
        public static float GetCooldownRemaining(Player player, int skillId)
        {
            if (player == null || !Cooldowns.TryGetValue(player.UserId, out var map)) return 0f;
            if (!map.TryGetValue(skillId, out var time)) return 0f;
            return (float)(time - DateTime.UtcNow).TotalSeconds;
        }

        private static void SetCooldown(Player player, int skillId, float seconds)
        {
            if (!Cooldowns.TryGetValue(player.UserId, out var map))
            {
                map = new Dictionary<int, DateTime>();
                Cooldowns[player.UserId] = map;
            }
            map[skillId] = DateTime.UtcNow.AddSeconds(seconds);
        }

        /// <summary>重置所有技能状态(新回合调用)</summary>
        public static void Reset()
        {
            Cooldowns.Clear();
            ActiveSkill2.Clear();
        }
    }
}
