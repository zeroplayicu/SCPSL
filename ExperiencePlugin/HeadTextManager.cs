using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;

namespace ExperiencePlugin
{
    /// <summary>
    /// 玩家头顶文字管理器（v2）。
    ///
    /// 【修复说明】v1 使用服务器端 new GameObject + TextMesh 创建文字，
    /// 但 SCP:SL 是专用服务器，服务器端创建的非网络 GameObject 不会同步到客户端，
    /// 因此玩家永远看不到头顶文字。v2 改为使用游戏自带的网络昵称系统
    /// Player.DisplayNickname（对应 nicknameSync.Network_displayName），
    /// 该字段是网络同步的，所有客户端必然能在玩家头顶看到该文字。
    /// </summary>
    public class HeadTextManager
    {
        private readonly ExperiencePlugin _plugin;

        /// <summary>玩家 UserId → 当前头顶富文本文字</summary>
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

        /// <summary>玩家 UserId → 设置前的原始 DisplayNickname（清除时恢复）</summary>
        private readonly Dictionary<string, string> _originalNames = new Dictionary<string, string>();

        public HeadTextManager(ExperiencePlugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>设置玩家头顶文字（支持富文本：颜色/字号）。文字为空则清除。</summary>
        public void SetHeadText(Player player, string text, string colorHex = "#FFFFFF", int fontSize = 30)
        {
            if (player == null || !player.IsConnected) return;

            try
            {
                // 先清除旧的（恢复原始昵称），再设置新的
                RemoveHeadText(player);

                if (string.IsNullOrWhiteSpace(text))
                    return;

                // 记录原始昵称，仅记录第一次，避免重复设置时覆盖成自定义文字
                if (!_originalNames.ContainsKey(player.UserId))
                    _originalNames[player.UserId] = player.DisplayNickname ?? player.Nickname;

                int size = Math.Max(10, Math.Min(80, fontSize));
                string rich = $"<color={colorHex}><size={size}>{text}</size></color>";

                player.DisplayNickname = rich;
                _texts[player.UserId] = rich;

                if (_plugin.Config.Debug)
                    Log.Debug($"[头顶文字] 已设置 {player.Nickname}: {rich}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[头顶文字] 设置失败 {player.Nickname}: {ex.Message}");
            }
        }

        /// <summary>清除指定玩家的头顶文字</summary>
        public void RemoveHeadText(Player player)
        {
            if (player == null) return;
            RemoveHeadText(player.UserId);
        }

        public void RemoveHeadText(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            if (_texts.Remove(userId))
            {
                try
                {
                    var p = Player.Get(userId);
                    if (p != null && p.IsConnected)
                    {
                        p.DisplayNickname = _originalNames.TryGetValue(userId, out var orig)
                            ? orig
                            : p.Nickname;
                    }
                }
                catch { }
                _originalNames.Remove(userId);
            }
        }

        /// <summary>清除所有头顶文字（回合结束/插件禁用时调用）</summary>
        public void ClearAll()
        {
            foreach (var userId in _texts.Keys.ToList())
                RemoveHeadText(userId);
            _originalNames.Clear();
        }

        /// <summary>回合结束时清理（防止文字跨回合残留）</summary>
        public void OnRoundEnd()
        {
            ClearAll();
        }

        /// <summary>插件禁用时清理</summary>
        public void Shutdown()
        {
            ClearAll();
        }

        /// <summary>检查玩家是否已有头顶文字</summary>
        public bool HasText(string userId)
        {
            return !string.IsNullOrEmpty(userId) && _texts.ContainsKey(userId);
        }

        /// <summary>
        /// 玩家重连/换角色/重生后重新应用头顶文字。
        /// 游戏会在玩家重新验证或切换角色时把 DisplayNickname 重置为默认昵称，
        /// 需要在此处把之前设置的文字重新写回。
        /// </summary>
        public void Reapply(Player player)
        {
            if (player == null || !player.IsConnected) return;

            if (_texts.TryGetValue(player.UserId, out var rich))
            {
                try
                {
                    if (!_originalNames.ContainsKey(player.UserId))
                        _originalNames[player.UserId] = player.DisplayNickname ?? player.Nickname;
                    player.DisplayNickname = rich;
                }
                catch (Exception ex)
                {
                    Log.Debug($"[头顶文字] 重应用失败 {player.Nickname}: {ex.Message}");
                }
            }
        }
    }
}
