using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Features.Objects;
using Scp999.Features.Manager;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Scp999.Features.Controller;
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// Register features for the player
    /// </summary>
    void Awake()
    {
        _player = Player.Get(gameObject);
        Config config = Plugin.Singleton.Config;

        _schematicObject = SchematicManager.AddSchematicByName(config.SchematicName); // Create schematic

        // 本轮修复: SchematicManager.AddSchematicByName 加载失败时返回 null。
        // 原实现继续往下走，MovementController.Update 每帧访问 _schematicObject.transform
        // 会抛 NullReferenceException（每帧刷错误日志）。加载失败时直接终止组件初始化。
        if (_schematicObject == null)
        {
            Log.Error($"[PlayerController] Schematic '{config.SchematicName}' failed to load, skip controller setup for {_player.Nickname}");
            Destroy(this);
            return;
        }

        _animator = SchematicManager.GetAnimatorFromSchematic(_schematicObject); // Get animator from schematic
        _audioPlayer = AudioManager.AddAudioPlayer(_player, config.Volume);      // Create audioPlayer
        _audioPlayer.TryGetSpeaker("scp999-speaker", out Speaker speaker);       // Get speaker
        _textToy = TextToyManager.CreateTextForSchematic(_player, _schematicObject); // Create TextToy

        _movementController = gameObject.AddComponent<MovementController>();
        _movementController.Init(_schematicObject, speaker, config.SchematicOffset);
        _cooldownController = gameObject.AddComponent<CooldownController>();

        Timing.CallDelayed(0.1f, () =>
        {
            _hintController = gameObject.AddComponent<HintController>();
            _hintController.Init(_player);
        });

        Log.Debug($"[PlayerController] Custom role granted for {_player.Nickname}");
    }

    /// <summary>
    /// Unregister features for the player
    /// </summary>
    void OnDestroy()
    {
        Destroy(_hintController);     // Destroy hints
        Destroy(_movementController); // Destroy movement controller for schematic and audio
        Destroy(_cooldownController); // Destroy cooldown for abilities

        // 本轮修复: 若 Awake 在创建 audioPlayer/schematic 之前提前返回（加载失败路径），
        // 这些字段为 null，直接调用会抛 NRE
        if (_audioPlayer != null)
        {
            _audioPlayer.RemoveAllClips();          // Remove all audio clips
            _audioPlayer.Destroy();                 // Remove a AudioPlayer
        }

        if (_schematicObject != null)
            _schematicObject.Destroy();             // Remove schematic

        Log.Debug($"[PlayerController] Custom role removed for {_player.Nickname}");
    }
    
    // Properties
    public Animator GetCurrentAnimator => this._animator;
    public AudioPlayer GetCurrentAudioPlayer => this._audioPlayer;

    // Fields
    private Player _player;
    private SchematicObject _schematicObject;
    private Animator _animator;
    private AudioPlayer _audioPlayer;
    private TextToy _textToy;
    private IEnumerable<SettingBase> _settings;
    private MovementController _movementController;
    private CooldownController _cooldownController;
    private HintController _hintController;
}