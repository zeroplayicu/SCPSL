using System.Text.RegularExpressions;

namespace ChatPlugin
{
    /// <summary>
    /// UI-03修复: 玩家输入富文本过滤。
    /// 玩家在 .bc / .c 中输入的文本会被直接嵌入富文本模板；若不过滤，
    /// 注入 &lt;size=2000&gt; 可把消息放大到占满屏幕、&lt;cspace=200&gt; 可拉爆排版，
    /// 未闭合标签的作用域还会延续到同一屏堆叠的后续消息，导致整屏格式错乱。
    /// </summary>
    public static class RichTextSanitizer
    {
        private static readonly Regex TagPattern = new Regex("<[^>]*>", RegexOptions.Compiled);

        /// <summary>剥离富文本标签形状的片段，并把残余尖括号替换为全角，杜绝注入</summary>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            string stripped = TagPattern.Replace(input, "");
            return stripped.Replace('<', '‹').Replace('>', '›');
        }
    }
}
