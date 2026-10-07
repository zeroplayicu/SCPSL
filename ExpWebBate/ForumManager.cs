using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExpWebBate
{
    public class ForumManager
    {
        private readonly string _basePath;
        private readonly object _lock = new object();

        public ForumManager(string dataDirectory)
        {
            _basePath = Path.Combine(dataDirectory, "forum");
            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);
        }

        private string GetPostsFile() => Path.Combine(_basePath, "posts.json");
        private string GetReportsFile() => Path.Combine(_basePath, "reports.json");
        private string GetEvidenceDir() => Path.Combine(_basePath, "evidence");

        // ========== 帖子系统 ==========

        public List<PostEntry> GetPosts(bool onlyApproved = true, int page = 1, int pageSize = 20)
        {
            var all = LoadPosts();
            var query = onlyApproved ? all.Where(p => p.Approved) : all;
            return query.OrderByDescending(p => p.CreatedAt)
                       .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        public PostEntry GetPost(string postId)
        {
            return LoadPosts().FirstOrDefault(p => p.Id == postId);
        }

        public bool CreatePost(PostEntry post)
        {
            lock (_lock)
            {
                var list = LoadPosts();
                list.Add(post);
                SavePosts(list);
                return true;
            }
        }

        public bool ApprovePost(string postId, bool approved, string adminName)
        {
            lock (_lock)
            {
                var list = LoadPosts();
                var post = list.FirstOrDefault(p => p.Id == postId);
                if (post == null) return false;
                post.Approved = approved;
                post.ReviewedBy = adminName;
                post.ReviewedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                SavePosts(list);
                return true;
            }
        }

        public bool DeletePost(string postId)
        {
            lock (_lock)
            {
                var list = LoadPosts();
                var removed = list.RemoveAll(p => p.Id == postId);
                if (removed > 0) { SavePosts(list); return true; }
                return false;
            }
        }

        // ========== 举报系统 ==========

        public List<ReportEntry> GetReports(bool onlyPending = false, int page = 1, int pageSize = 20)
        {
            var all = LoadReports();
            var query = onlyPending ? all.Where(r => r.Status == "pending") : all;
            return query.OrderByDescending(r => r.CreatedAt)
                       .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        public ReportEntry GetReport(string reportId)
        {
            return LoadReports().FirstOrDefault(r => r.Id == reportId);
        }

        public bool CreateReport(ReportEntry report)
        {
            lock (_lock)
            {
                var list = LoadReports();
                list.Add(report);
                SaveReports(list);
                return true;
            }
        }

        public bool ProcessReport(string reportId, string action, string adminName)
        {
            lock (_lock)
            {
                var list = LoadReports();
                var report = list.FirstOrDefault(r => r.Id == reportId);
                if (report == null) return false;
                report.Status = action; // "approved" 或 "rejected"
                report.ProcessedBy = adminName;
                report.ProcessedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                SaveReports(list);
                return true;
            }
        }

        // ========== 证据存储（base64图片/视频链接） ==========

        public string SaveEvidence(string base64Data, string fileName)
        {
            lock (_lock)
            {
                if (!Directory.Exists(GetEvidenceDir()))
                    Directory.CreateDirectory(GetEvidenceDir());

                string ext = ".jpg";
                if (fileName != null)
                {
                    string fext = Path.GetExtension(fileName)?.ToLower();
                    if (fext == ".png" || fext == ".jpg" || fext == ".jpeg" || fext == ".gif" || fext == ".mp4" || fext == ".webm")
                        ext = fext;
                }

                string fileId = Guid.NewGuid().ToString("N");
                string filePath = Path.Combine(GetEvidenceDir(), fileId + ext);

                try
                {
                    byte[] bytes = Convert.FromBase64String(base64Data);
                    File.WriteAllBytes(filePath, bytes);
                    return fileId + ext;
                }
                catch
                {
                    // 如果是 URL 链接，直接保存
                    File.WriteAllText(filePath + ".url", base64Data);
                    return fileId + ext + ".url";
                }
            }
        }

        public byte[] GetEvidence(string fileName)
        {
            string path = Path.Combine(GetEvidenceDir(), fileName);
            if (!File.Exists(path)) return null;
            try { return File.ReadAllBytes(path); }
            catch { return null; }
        }

        // ========== 序列化 ==========

        private List<PostEntry> LoadPosts()
        {
            string path = GetPostsFile();
            if (!File.Exists(path)) return new List<PostEntry>();
            try { return FastJson.DeserializeList<PostEntry>(File.ReadAllText(path)); }
            catch { return new List<PostEntry>(); }
        }

        private void SavePosts(List<PostEntry> list)
        {
            File.WriteAllText(GetPostsFile(), FastJson.SerializeList(list));
        }

        private List<ReportEntry> LoadReports()
        {
            string path = GetReportsFile();
            if (!File.Exists(path)) return new List<ReportEntry>();
            try { return FastJson.DeserializeList<ReportEntry>(File.ReadAllText(path)); }
            catch { return new List<ReportEntry>(); }
        }

        private void SaveReports(List<ReportEntry> list)
        {
            File.WriteAllText(GetReportsFile(), FastJson.SerializeList(list));
        }
    }

    // ========== 数据模型 ==========

    public class PostEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string CreatedAt { get; set; }
        public bool Approved { get; set; }
        public string ReviewedBy { get; set; }
        public string ReviewedAt { get; set; }
        public string Category { get; set; } // "general", "suggestion", "bug", "other"
        public int Views { get; set; }
        public int Replies { get; set; }
    }

    public class ReportEntry
    {
        public string Id { get; set; }
        public string ReporterId { get; set; }
        public string ReporterName { get; set; }
        public string TargetId { get; set; }   // 被举报人 SteamId
        public string TargetName { get; set; }  // 被举报人昵称
        public string Reason { get; set; }
        public string Description { get; set; }
        public string EvidenceUrl { get; set; }     // 证据URL
        public string EvidenceFile { get; set; }    // 证据文件名
        public string CreatedAt { get; set; }
        public string Status { get; set; }     // "pending", "approved", "rejected"
        public string ProcessedBy { get; set; }
        public string ProcessedAt { get; set; }
        public string ServerInfo { get; set; }
    }

    // ========== 简易 JSON 序列化 ==========

    internal static class FastJson
    {
        public static string SerializeList<T>(List<T> list) where T : class
        {
            var items = new List<string>();
            foreach (var item in list)
                items.Add(Serialize(item));
            return "[" + string.Join(",", items) + "]";
        }

        public static string Serialize<T>(T obj) where T : class
        {
            if (obj == null) return "{}";
            var type = typeof(T);
            var props = type.GetProperties();
            var parts = new List<string>();
            foreach (var p in props)
            {
                var val = p.GetValue(obj);
                string strVal;
                if (val == null) strVal = "null";
                else if (val is bool b) strVal = b ? "true" : "false";
                else if (val is int || val is long || val is float || val is double)
                    strVal = val.ToString().ToLower();
                else strVal = "\"" + Escape(val.ToString()) + "\"";
                parts.Add("\"" + p.Name + "\":" + strVal);
            }
            return "{" + string.Join(",", parts) + "}";
        }

        public static List<T> DeserializeList<T>(string json) where T : class, new()
        {
            var list = new List<T>();
            if (string.IsNullOrEmpty(json) || json.Trim() == "[]") return list;
            string inner = json.Trim().TrimStart('[').TrimEnd(']');
            int depth = 0;
            int start = -1;
            for (int i = 0; i < inner.Length; i++)
            {
                char c = inner[i];
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { list.Add(Deserialize<T>(inner.Substring(start, i - start + 1))); start = -1; } }
            }
            return list;
        }

        public static T Deserialize<T>(string json) where T : class, new()
        {
            var obj = new T();
            if (string.IsNullOrEmpty(json) || json.Trim() == "{}") return obj;
            string inner = json.Trim().TrimStart('{').TrimEnd('}');
            var type = typeof(T);
            var props = type.GetProperties().ToDictionary(p => p.Name, p => p);

            int i = 0;
            while (i < inner.Length)
            {
                while (i < inner.Length && inner[i] <= ' ') i++;
                if (i >= inner.Length) break;
                if (inner[i] != '"') break;
                i++; int keyStart = i;
                while (i < inner.Length && inner[i] != '"') { if (inner[i] == '\\') i++; i++; }
                string key = inner.Substring(keyStart, i - keyStart);
                i++;
                while (i < inner.Length && inner[i] != ':') i++;
                i++;
                while (i < inner.Length && inner[i] <= ' ') i++;
                if (i >= inner.Length) break;

                if (props.TryGetValue(key, out var prop))
                {
                    object val = null;
                    if (inner[i] == '"')
                    {
                        i++; int vStart = i;
                        while (i < inner.Length && inner[i] != '"') { if (inner[i] == '\\') i++; i++; }
                        val = inner.Substring(vStart, i - vStart);
                        i++;
                    }
                    else if (inner[i] == 't' && inner.Substring(i).StartsWith("true")) { val = true; i += 4; }
                    else if (inner[i] == 'f' && inner.Substring(i).StartsWith("false")) { val = false; i += 5; }
                    else if (inner[i] == 'n' && inner.Substring(i).StartsWith("null")) { val = null; i += 4; }
                    else
                    {
                        int nStart = i;
                        while (i < inner.Length && inner[i] != ',' && inner[i] != '}' && inner[i] != ']') i++;
                        string num = inner.Substring(nStart, i - nStart).Trim();
                        if (prop.PropertyType == typeof(int)) val = int.Parse(num);
                        else if (prop.PropertyType == typeof(long)) val = long.Parse(num);
                        else if (prop.PropertyType == typeof(float)) val = float.Parse(num);
                        else if (prop.PropertyType == typeof(double)) val = double.Parse(num);
                        else val = num;
                    }
                    if (val != null) prop.SetValue(obj, val);
                }
                while (i < inner.Length && inner[i] != ',') i++;
                i++;
            }
            return obj;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }
    }
}
