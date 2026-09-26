using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using MEC;
using Scp999.Interfaces;
using UnityEngine;

namespace Scp999.Features.Abilities;
public class AnimationAbility : Ability
{
    public override string Name => "技能3";
    public override string Description => "随机播放一个搞笑动画，动画期间无法移动（冷却15秒）";
    public override int KeyId => 9993;
    public override KeyCode KeyCode => KeyCode.None;
    public override KeyCode SuggestedKey => KeyCode.None;
    public override float Cooldown => 15f;
    protected override void ActivateAbility(Player player, Animator animator, AudioPlayer audioPlayer)
    {
        player.EnableEffect<Ensnared>(30f);
        
        int rand = Random.Range(0, 100) + 1;
        switch (rand)
        {
            // throwing balls
            case > 0 and <= 15:
            {
                animator?.Play($"FunAnimation1");
                audioPlayer?.AddClip($"circus"); 
            } break;
            
            // Jump x3
            case > 15 and <= 60:
            {
                animator?.Play($"FunAnimation2");
                audioPlayer?.AddClip($"jump"); 
            } break;
            
            // Shrinking
            case > 60 and <= 90:
            {
                animator?.Play($"FunAnimation3");
                audioPlayer?.AddClip($"funnytoy"); 
            } break;
            
            // UwU - Secret animation
            case > 90:
            {
                animator?.Play($"FunAnimation4");
                audioPlayer?.AddClip($"uwu"); 
            } break;
        }
        
        Timing.RunCoroutine(this.CheckEndOfAnimation(player, animator));
    }

    private IEnumerator<float> CheckEndOfAnimation(Player player, Animator animator)
    {
        yield return Timing.WaitForSeconds(0.1f);

        // 本轮修复: 角色被移除/玩家死亡时 animator 会被销毁（Unity 伪 null），
        // GetCurrentAnimatorClipInfo 也可能返回空数组——原实现会抛
        // MissingReferenceException / IndexOutOfRangeException，且跳过 Ensnared 的解除。
        if (animator == null)
            yield break;

        var initialClips = animator.GetCurrentAnimatorClipInfo(0);
        if (initialClips.Length == 0)
            yield break;

        string initialClipName = initialClips[0].clip.name;

        while (true)
        {
            if (player == null || !player.IsConnected || animator == null)
                yield break;

            var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length == 0 || clipInfo[0].clip.name != initialClipName)
            {
                player.DisableEffect<Ensnared>();
                yield break;
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }
}