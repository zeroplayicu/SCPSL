using UnityEngine;

namespace Scp999.Interfaces;
public interface IAbility
{
    string Name { get; }
    string Description { get; }
    int KeyId { get; }
    KeyCode KeyCode { get; }

    /// <summary>
    /// 建议的默认按键（显示在"服务器专属设置"里）。
    /// 返回 KeyCode.None 表示不预设默认键，由玩家自行绑定。
    /// </summary>
    KeyCode SuggestedKey { get; }
    float Cooldown { get; }
    void Register();
    void Unregister();
}