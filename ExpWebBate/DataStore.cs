using System;
using System.Collections.Generic;
using System.IO;
using Exiled.API.Features;

namespace ExpWebBate
{
    public class DataStore
    {
        private readonly string _basePath;

        public DataStore(ExpWebConfig config)
        {
            _basePath = Path.Combine(Paths.Configs, "ExpWebBate");
        }

        public void Initialize()
        {
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        private string GetPath(string name)
        {
            return Path.Combine(_basePath, name + ".json");
        }

        // ========== 管理员 ==========
        public Dictionary<string, object> LoadAdmin()
        {
            string path = GetPath("admin");
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                return FastJsonDeserialize(json);
            }
            catch { return null; }
        }

        public void SaveAdmin(Dictionary<string, object> data)
        {
            string path = GetPath("admin");
            File.WriteAllText(path, FastJsonSerialize(data));
        }

        // ========== 抽奖记录 ==========
        public List<Dictionary<string, object>> LoadLotteryLog()
        {
            string path = GetPath("lottery_log");
            if (!File.Exists(path)) return new List<Dictionary<string, object>>();
            try
            {
                string json = File.ReadAllText(path);
                return FastJsonDeserializeList(json);
            }
            catch { return new List<Dictionary<string, object>>(); }
        }

        public void AppendLotteryLog(Dictionary<string, object> entry)
        {
            var list = LoadLotteryLog();
            list.Add(entry);
            // 只保留最近 500 条
            if (list.Count > 500)
                list.RemoveAt(0);
            File.WriteAllText(GetPath("lottery_log"), FastJsonSerializeList(list));
        }

        // ========== CDK 记录 ==========
        public void AppendRedeemLog(string playerId, string code)
        {
            string path = GetPath("redeem_log");
            var list = new List<string>();
            if (File.Exists(path))
            {
                try { list = new List<string>(File.ReadAllLines(path)); }
                catch { }
            }
            list.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "|" + playerId + "|" + code);
            if (list.Count > 1000) list.RemoveRange(0, list.Count - 1000);
            File.WriteAllLines(path, list);
        }

        // ========== 简易 JSON 序列化 ==========
        private static string FastJsonSerialize(Dictionary<string, object> data)
        {
            if (data == null || data.Count == 0) return "{}";
            var parts = new List<string>();
            foreach (var kvp in data)
                parts.Add("\"" + EscapeJson(kvp.Key) + "\":\"" + EscapeJson(kvp.Value?.ToString()) + "\"");
            return "{" + string.Join(",", parts) + "}";
        }

        private static string FastJsonSerializeList(List<Dictionary<string, object>> list)
        {
            var items = new List<string>();
            foreach (var d in list)
                items.Add(FastJsonSerialize(d));
            return "[" + string.Join(",", items) + "]";
        }

        private static Dictionary<string, object> FastJsonDeserialize(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(json) || json.Trim() == "{}") return result;
            string inner = json.Trim().TrimStart('{').TrimEnd('}');
            int i = 0;
            while (i < inner.Length)
            {
                // 跳过空格
                while (i < inner.Length && inner[i] <= ' ') i++;
                if (i >= inner.Length) break;
                // 读取 key
                if (inner[i] != '"') break;
                i++;
                int keyStart = i;
                while (i < inner.Length && inner[i] != '"') { if (inner[i] == '\\') i++; i++; }
                string key = inner.Substring(keyStart, i - keyStart);
                i++;
                // 跳过分隔符
                while (i < inner.Length && inner[i] != ':') i++;
                i++;
                while (i < inner.Length && inner[i] <= ' ') i++;
                // 读取 value
                if (i < inner.Length && inner[i] == '"')
                {
                    i++;
                    int valStart = i;
                    while (i < inner.Length && inner[i] != '"') { if (inner[i] == '\\') i++; i++; }
                    string val = inner.Substring(valStart, i - valStart);
                    result[key] = val;
                    i++;
                }
                // 跳过逗号
                while (i < inner.Length && inner[i] != ',') i++;
                i++;
            }
            return result;
        }

        private static List<Dictionary<string, object>> FastJsonDeserializeList(string json)
        {
            var result = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(json) || json.Trim() == "[]") return result;
            string inner = json.Trim().TrimStart('[').TrimEnd(']');
            int depth = 0;
            int start = -1;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { result.Add(FastJsonDeserialize(inner.Substring(start, i - start + 1))); start = -1; } }
            }
            return result;
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
