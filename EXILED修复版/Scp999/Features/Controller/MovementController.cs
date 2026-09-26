using Exiled.API.Features;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace Scp999.Features.Controller;
public class MovementController : MonoBehaviour
{
    public void Init(SchematicObject schematicObject, Speaker speaker, Vector3 offset)
    {
        _player = Player.Get(gameObject);
        _schematicObject = schematicObject;
        _speaker = speaker;
        _offset = offset;
        
        Log.Debug($"[ObjectController] Init the controller");
    }

    private void Update()
    {
        // 本轮修复: 判空保护——
        //  1. Init 传入的 speaker 可能为 null（TryGetSpeaker 失败时），原实现每帧访问
        //     _speaker.transform 会抛 NullReferenceException；
        //  2. 玩家/schematic 已被销毁（Unity 伪 null）时也要跳过，避免 MissingReferenceException。
        if (_player == null || _schematicObject == null)
            return;

        _schematicObject.transform.position = _player.GameObject.transform.position + _offset;
        _schematicObject.transform.rotation = _player.GameObject.transform.rotation;

        if (_speaker != null)
            _speaker.transform.position = _player.GameObject.transform.position;
    }

    private void OnDestroy()
    {
        _schematicObject = null;
        _player = null;
        _speaker = null;
        
        Log.Debug($"[ObjectController] Destroy the controller");
    }

    private SchematicObject _schematicObject;
    private Player _player;
    private Speaker _speaker;
    private Vector3 _offset;
}