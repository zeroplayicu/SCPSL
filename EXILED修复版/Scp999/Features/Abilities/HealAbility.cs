using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using Scp999.Interfaces;
using UnityEngine;

namespace Scp999.Features.Abilities;
public class HealAbility : Ability
{
    public override string Name => "主技能";
    public override string Description => "治疗你周围的所有玩家（冷却60秒）";
    public override int KeyId => 9992;
    public override KeyCode KeyCode => KeyCode.None;
    public override KeyCode SuggestedKey => KeyCode.None;
    public override float Cooldown => 60f;
    protected override void ActivateAbility(Player player, Animator animator, AudioPlayer audioPlayer)
    {
        animator?.Play("HealthAnimation");
        // 本轮修复: audioPlayer 可能为 null（与 AnimationAbility/YippeeAbility 的判空保持一致）
        audioPlayer?.AddClip($"health");
        
        // Heal all the players in the radius
        foreach (Player ply in Player.List)
        {
            if (player == ply)
                continue;
            
            if (Vector3.Distance(player.Position, ply.Position) < Plugin.Singleton.Config.MaxDistance)
            {
                float value = ply.MaxHealth >= 2000 ? 500 : 100;
                ply.Heal(value);
            }
        }
        
        //Timing.RunCoroutine(this.CheckEndOfAnimation(player, animator));
    }

    private IEnumerator<float> CheckEndOfAnimation(Player player, Animator animator)
    {
        //yield return Timing.WaitForSeconds(1f);
        float distance = Plugin.Singleton.Config.MaxDistance;
        
        while (true)
        {
            // If the animation has returned to the Idle state, then break the cycle
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName("HealthAnimation"))
                yield break;
            
            // Heal all the players in the radius
            foreach (Player ply in Player.List)
            {
                if (player == ply)
                    continue;
            
                if (Vector3.Distance(player.Position, ply.Position) < distance)
                {
                    float value;
                    if (ply.MaxHealth >= 2000)
                    {
                        value = 200; //SCP
                    }
                    else
                    {
                        value = 20; //Human
                    }
                    
                    ply.Heal(value);
                }
            }
            
            // We are waiting for 2 seconds, as less is worse
            Log.Debug("[HealAbility] Heal all players");
            yield return Timing.WaitForSeconds(1f);
        }
    }
}