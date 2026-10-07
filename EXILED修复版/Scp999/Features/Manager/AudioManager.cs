using System.Collections.Generic;
using System.IO;
using Exiled.API.Features;

namespace Scp999.Features.Manager;
public static class AudioManager
{
    private static List<string> _audioNameList = new()
    {
        "yippee-tbh1",
        "yippee-tbh2",
        "hello",
        "hi",
        "jump",
        "health",
        "circus",
        "funnytoy",
        "uwu"
    };
    
    public static AudioPlayer AddAudioPlayer(Player player, int volume)
    {
        // 本轮修复: 原实现用昵称作 Key，游戏允许玩家中途改昵称——改名后会再建一个新
        // AudioPlayer（旧的成为泄漏），两个同名玩家则会共享同一播放器导致声音错乱。
        // UserId 在整个会话中唯一且稳定，改用它作 Key。
        AudioPlayer audioPlayer = AudioPlayer.CreateOrGet($"Scp999 {player.UserId}", onIntialCreation: (p) =>
        {
            // Attach created audio player to player.
            p.transform.parent = player.GameObject.transform;

            // This created speaker will be in 3D space.
            Speaker speaker = p.AddSpeaker("scp999-speaker", volume, true, 5f, 15f);
        });

        LoadAudioFiles();
        
        return audioPlayer;
    }

    private static void LoadAudioFiles()
    {
        string path = Plugin.Singleton.AudioPath;
        
        foreach (string audioName in _audioNameList)
        {
            if (!AudioClipStorage.AudioClips.ContainsKey(audioName))
            {
                string filePath = Path.Combine(path, audioName) + ".ogg";
            
                if (!AudioClipStorage.LoadClip(filePath, audioName))
                {
                    Log.Error($"[AddAudioPlayer] The audio file {audioName} was not found for playback");
                }
            }
        }
    }
}