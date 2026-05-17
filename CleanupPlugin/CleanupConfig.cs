using System.ComponentModel;

namespace CleanupPlugin
{
    public class CleanupConfig
    {
        public bool Debug { get; set; } = false;

        [Description("触发清理的掉落物数量阈值")]
        public int CleanupThreshold { get; set; } = 250;

        [Description("掉落物数量检测间隔（秒）")]
        public int CheckInterval { get; set; } = 10;

        [Description("倒计时时长（秒）")]
        public int CountdownSeconds { get; set; } = 5;

        [Description("倒计时公告前缀")]
        public string CountdownPrefix { get; set; } = "<color=red>⚠ 我要扫地了抬抬脚</color>";

        [Description("倒计时模板（{time}=剩余秒数）")]
        public string CountdownTemplate { get; set; } =
            "<size=35><color=#FF4444>⚠ 我要扫地了抬抬脚</color></size>\n" +
            "<size=50><color=yellow>{time}</color></size>";

        [Description("清理完成提示")]
        public string CleanupDoneMessage { get; set; } =
            "<color=green>✅ 扫地完成！已清空所有掉落物和尸体</color>";

        [Description("是否同时清理尸体（ragdoll）")]
        public bool CleanRagdolls { get; set; } = true;

        [Description("受保护的SCP物品类型（逗号分隔，不会被清理）")]
        public string ProtectedItemTypes { get; set; } = "SCP500,SCP207,SCP268,SCP1853,SCP1576,SCP127,SCP1344,SCP330";
    }
}
