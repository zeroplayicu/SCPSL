using System;
using System.Collections;
using System.Linq;
using System.Text;
using CommandSystem;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;

namespace ChatPlugin.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BuffCommand : ICommand
    {
        public string Command => "buff";
        public string[] Aliases => new[] { "effects", "e" };
        public string Description => "查看当前所有活跃效果和剩余时间";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                string playerName = sender.LogName;
                int spaceIdx = playerName.IndexOf(" (", StringComparison.Ordinal);
                if (spaceIdx > 0) playerName = playerName.Substring(0, spaceIdx);

                var player = Player.List?.FirstOrDefault(p => p != null && p.Nickname == playerName);
                if (player == null)
                {
                    response = "无法获取玩家信息";
                    return false;
                }

                var sb = new StringBuilder();
                sb.AppendLine("<size=20><color=#FFD700>══ 当前效果 ══</color></size>");
                int count = 0;

                // 通过全反射获取效果列表
                try
                {
                    var refHub = player.GetType().GetProperty("ReferenceHub")?.GetValue(player);
                    var fxCtrl = refHub?.GetType().GetProperty("playerEffectsController")?.GetValue(refHub);
                    var allEffects = fxCtrl?.GetType().GetProperty("AllEffects")?.GetValue(fxCtrl) as IEnumerable;
                    if (allEffects != null)
                    {
                        foreach (var effectObj in allEffects)
                        {
                            if (effectObj == null) continue;
                            var et = effectObj.GetType();
                            var isEnabledProp = et.GetProperty("IsEnabled");
                            if (isEnabledProp == null || !(bool)isEnabledProp.GetValue(effectObj)) continue;

                            float timeLeft = 0f;
                            var tlProp = et.GetProperty("TimeLeft");
                            if (tlProp != null) timeLeft = (float)tlProp.GetValue(effectObj);

                            int intensity = 0;
                            var intProp = et.GetProperty("Intensity");
                            if (intProp != null) intensity = Convert.ToInt32(intProp.GetValue(effectObj));

                            count++;
                            string name = GetEffectName(et.Name);
                            int remaining = (int)Math.Ceiling(timeLeft);

                            sb.AppendLine($"<color=#00FF00>• {name}</color> <color=white>强度{intensity} | 剩余{remaining}秒</color>");
                        }
                    }
                }
                catch { }

                if (count == 0)
                    sb.AppendLine("<color=gray>当前没有任何活跃效果</color>");

                if (ChatPlugin.Instance.Config.LogChat)
                    Logger.Info($"[Buff] {playerName}: 查看了效果 ({count}个)");

                response = sb.ToString();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Buff命令错误: {ex.Message}");
                response = "获取效果失败";
                return false;
            }
        }

        private static string GetEffectName(string englishName) => englishName switch
        {
            "MovementBoost" => "移速增强", "Scp207" => "SCP-207", "Scp500" => "SCP-500",
            "Scp1344" => "SCP-1344", "Scp1853" => "SCP-1853", "Scp268" => "SCP-268",
            "Scp513" => "SCP-513", "AmnesiaItems" => "记忆丧失", "Asphyxiating" => "窒息",
            "Bleeding" => "流血", "Burned" => "烧伤", "Concussed" => "震荡",
            "Corroding" => "腐蚀", "Deafened" => "失聪", "Decontaminating" => "净化",
            "Disabled" => "瘫痪", "Ensnared" => "困缚", "Exhausted" => "疲劳",
            "Flashed" => "致盲", "Hemorrhage" => "大出血", "Hypothermia" => "低温",
            "Invigorated" => "振奋", "Poisoned" => "中毒", "SinkHole" => "陷阱",
            "Soundless" => "沉默", "Vitality" => "活力", "DamageReduction" => "减伤",
            "CardiacArrest" => "心脏骤停", "BodilyInjury" => "肢体损伤",
            _ => englishName
        };
    }
}
