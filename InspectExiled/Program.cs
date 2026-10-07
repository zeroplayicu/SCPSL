using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// 通用程序集元数据探查工具：
// 用法: InspectExiled <程序集路径> <类型名关键字>
// 列出匹配类型的所有 字段/属性/方法。
class Program
{
    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : @"z:\codebubby\SCPSL pulint\zeropl\ex\Assembly-CSharp-Publicized.dll";
        string filter = args.Length > 1 ? args[1] : "";

        if (!File.Exists(path))
        {
            Console.WriteLine("dll not found: " + path);
            return 1;
        }
        Console.WriteLine("Reading: " + path);

        using var pe = new PEReader(File.OpenRead(path));
        var mr = pe.GetMetadataReader();

        foreach (var th in mr.TypeDefinitions)
        {
            var td = mr.GetTypeDefinition(th);
            var name = mr.GetString(td.Name);
            if (!string.IsNullOrEmpty(filter) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var ns = mr.GetString(td.Namespace);
            Console.WriteLine($"\n=== {ns}.{name} ===");

            foreach (var fh in td.GetFields())
            {
                var fd = mr.GetFieldDefinition(fh);
                Console.WriteLine($"  Field: {mr.GetString(fd.Name)}");
            }
            foreach (var ph in td.GetProperties())
            {
                var pd = mr.GetPropertyDefinition(ph);
                Console.WriteLine($"  Prop: {mr.GetString(pd.Name)}");
            }
            foreach (var mh in td.GetMethods())
            {
                var md = mr.GetMethodDefinition(mh);
                var mname = mr.GetString(md.Name);
                if (mname.StartsWith("get_") || mname.StartsWith("set_") || mname.StartsWith("ctor")) continue;
                Console.WriteLine($"  Method: {mname}");
            }
        }
        return 0;
    }
}
