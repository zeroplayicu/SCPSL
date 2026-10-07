using System.Collections.Generic;
using System.ComponentModel;
using Exiled.API.Interfaces;

namespace CleanupPlugin
{
    public class CleanupConfig : IConfig
    {
        [Description("插件是否启用")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式")]
        public bool Debug { get; set; } = false;

        [Description("清理间隔（秒），默认300秒(5分钟)")]
        public int CleanupInterval { get; set; } = 300;

        [Description("尸体数量超过该值时自动提前清扫")]
        // 本轮修复: 原描述写的是"掉落物数量"，但 OnThresholdCheck 实际检查的是尸体(Ragdoll)数量，
        // 启动日志也按"尸体"输出——按实际语义修正描述，避免运维误解配置含义
        public int PickupThreshold { get; set; } = 500;

        [Description("开局免清扫时间（秒）：回合开始后这段时间内不触发自动清扫，默认300秒(5分钟)")]
        public int GracePeriodSeconds { get; set; } = 300;

        [Description("警告倒计时（秒），在清理前显示提示")]
        public int WarningCountdown { get; set; } = 5;

        [Description("是否清理掉落物(pickup)。false=只清理尸体不清理掉落物")]
        public bool CleanPickups { get; set; } = false;

        [Description("是否清理尸体（ragdoll）")]
        public bool CleanRagdolls { get; set; } = true;

        [Description("清理警告提示模板（{time}=剩余秒数）")]
        public string WarningTemplate { get; set; } =
            "<size=35><color=#FF4444>我要扫地了抬抬脚</color></size>\n" +
            "<size=50><color=yellow>{time}</color></size>";

        [Description("清理完成提示")]
        public string CleanupDoneMessage { get; set; } =
            "<color=green>扫地完成！已清空所有尸体</color>";

        // 本轮修复: 原注释已说明"已取消 CASSIE 语音广播"，但下列 4 个 CASSIE 配置项
        // 在代码中零引用（grep 验证），改了配置不生效，属误导性配置。
        // 按照之前 BUG-14 的处理方式移除：UseCassieAnnouncement / CassieCountdownText /
        // CassieCleaningStartText / CassieCleaningDoneText。

        [Description("是否在CASSIE语音同时保留屏幕文字提示(默认true)")]
        public bool KeepScreenWarning { get; set; } = true;

        [Description("受保护的SCP物品类型（不会被清理），逗号分隔")]
        public string ProtectedItemTypes { get; set; } =
            "SCP500,SCP207,SCP268,SCP1853,SCP1576,SCP018,SCP330," +
            "MicroHID,SCP244a,SCP244b,SCP1507,Jailbird,AntiSCP207," +
            "SCP2176,GunSCP127,SCP1344";

        [Description("受保护的区域类型（物品不会被清理），逗号分隔RoomType名称")]
        public string ProtectedRoomTypes { get; set; } =
            "Lcz173,LczAirlock,LczArmory,LczCheckpointA,LczCheckpointB," +
            "LczClassDSpawn,LczGlassBox,LczGreenhouse,LczPlants," +
            "Hcz173,HczArmory,HczCheckpointA,HczCheckpointB," +
            "Hcz096,Hcz106,Hcz939,Hcz049,Hcz079,HczHid," +
            "HczMicroHID,HczServers,HczTestroom,HczWarhead," +
            "HczTesla,HczElevatorA,HczElevatorB," +
            "EzIntercom,EzCollapsedTunnel,EzVent";
    }
}
