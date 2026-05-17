namespace ChatPlugin
{
    public class ChatConfig
    {
        // ===== 全体聊天 (BC) =====
        public string BcPrefix { get; set; } = "<color=#FFD700>[全体]</color>";
        public ushort BcDuration { get; set; } = 5;
        public string BcAliases { get; set; } = "broadcast,all";

        // ===== 团队聊天 (C) =====
        public string CPrefix { get; set; } = "<color=#00BFFF>[团队]</color>";
        public ushort CDuration { get; set; } = 5;
        public string CAliases { get; set; } = "team,t";

        // ===== 通用 =====
        public string PlayerNameColor { get; set; } = "<color=white>";
        public string MessageColor { get; set; } = "<color=white>";
        public string FontSize { get; set; } = "18";
        public bool LogChat { get; set; } = true;
    }
}
