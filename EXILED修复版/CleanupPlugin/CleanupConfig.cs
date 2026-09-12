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

        [Description("掉落物数量超过该值时自动提前清扫")]
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

        [Description("是否使用CASSIE(系统)语音广播提示清理(默认true)")]
        public bool UseCassieAnnouncement { get; set; } = true;

        [Description("清理倒计时CASSIE语音内容({time}=剩余秒数的英文发音，CASSIE只支持英文)")]
        public string CassieCountdownText { get; set; } = "AREA WILL BE CLEANED IN .{time}";

        [Description("清理开始CASSIE语音内容")]
        public string CassieCleaningStartText { get; set; } = "CLEANING IN PROGRESS";

        [Description("清理完成CASSIE语音内容")]
        public string CassieCleaningDoneText { get; set; } = "CLEANING COMPLETED";

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
