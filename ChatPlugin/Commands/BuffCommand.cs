using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using CommandSystem;
using Exiled.API.Features;

namespace ChatPlugin.Commands
{
    /// <summary>
    /// 查看当前效果和剩余时间 - .buff / .effects / .e
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BuffCommand : ICommand
    {
        public string Command => "buff";
        public string[] Aliases => new[] { "effects", "e" };
        public string Description => "查看当前所有活跃效果和剩余时间";

        // 缓存反射获取的 PropertyInfo（只获取一次）
        private static PropertyInfo _activeEffectsProp;
        private static readonly object PropLock = new object();

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                var player = Player.List?.FirstOrDefault(p =>
                    p != null && (p.Nickname == playerName || p.Nickname == sender.LogName));
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                var sb = new StringBuilder();
                sb.AppendLine("<size=20><color=#FFD700>══ 当前效果 ══</color></size>");

                int count = 0;

                // 使用反射获取 ActiveEffects 属性，避免编译时依赖 StatusEffectBase (Unity 类型)
                var activeEffects = GetActiveEffects(player);
                if (activeEffects != null)
                {
                    foreach (object effect in activeEffects)
                    {
                        if (effect == null) continue;

                        var t = effect.GetType();

                        // IsEnabled (bool)
                        var isEnabledProp = t.GetProperty("IsEnabled",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (isEnabledProp == null) continue;
                        if (!(bool)isEnabledProp.GetValue(effect)) continue;

                        // TimeLeft (float)
                        float timeLeft = 0;
                        var timeLeftProp = t.GetProperty("TimeLeft",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (timeLeftProp != null)
                            timeLeft = (float)timeLeftProp.GetValue(effect);

                        // Intensity (byte)
                        int intensity = 0;
                        var intensityProp = t.GetProperty("Intensity",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (intensityProp != null)
                            intensity = Convert.ToInt32(intensityProp.GetValue(effect));

                        count++;
                        string name = GetEffectName(t.Name);
                        int remaining = (int)Math.Ceiling(timeLeft);

                        sb.AppendLine(
                            $"<color=#00FF00>• {name}</color> " +
                            $"<color=white>强度{intensity} | 剩余{remaining}秒</color>");
                    }
                }

                if (count == 0)
                    sb.AppendLine("<color=gray>当前没有任何活跃效果</color>");

                sender.Respond(sb.ToString());

                var cfg = ChatPlugin.Instance.Config;
                if (cfg.LogChat)
                    Log.Info($"[Buff] {playerName}: 查看了效果 ({count}个)");

                response = "";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Buff命令错误: {ex.Message}");
                response = "获取效果失败";
                return false;
            }
        }

        /// <summary>
        /// 通过反射获取玩家的 ActiveEffects 列表
        /// </summary>
        private static IEnumerable GetActiveEffects(Player player)
        {
            if (_activeEffectsProp == null)
            {
                lock (PropLock)
                {
                    if (_activeEffectsProp == null)
                    {
                        _activeEffectsProp = typeof(Player).GetProperty("ActiveEffects",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }
            return _activeEffectsProp?.GetValue(player) as IEnumerable;
        }

        private static string GetEffectName(string englishName)
        {
            return englishName switch
            {
                "MovementBoost" => "移速增强",
                "Scp207" => "SCP-207",
                "Scp500" => "SCP-500",
                "Scp1344" => "SCP-1344",
                "Scp1853" => "SCP-1853",
                "Scp268" => "SCP-268",
                "Scp513" => "SCP-513",
                "AmnesiaItems" => "记忆丧失",
                "Asphyxiating" => "窒息",
                "Bleeding" => "流血",
                "Burned" => "烧伤",
                "Concussed" => "震荡",
                "Corroding" => "腐蚀",
                "Deafened" => "失聪",
                "Decontaminating" => "净化",
                "Disabled" => "瘫痪",
                "Ensnared" => "困缚",
                "Exhausted" => "疲劳",
                "Flashed" => "致盲",
                "Hemorrhage" => "大出血",
                "Hypothermia" => "低温",
                "Invigorated" => "振奋",
                "Poisoned" => "中毒",
                "SinkHole" => "陷阱",
                "Soundless" => "沉默",
                "Vitality" => "活力",
                "DamageReduction" => "减伤",
                "CardiacArrest" => "心脏骤停",
                "BodilyInjury" => "肢体损伤",
                _ => englishName
            };
        }
    }
}
