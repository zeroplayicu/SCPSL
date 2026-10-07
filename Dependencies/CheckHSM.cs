using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(@"Z:\codebubby\SCPSL pulint\Dependencies\HintServiceMeow-Exiled.dll");
        foreach(var t in asm.GetExportedTypes().OrderBy(x => x.FullName))
        {
            Console.WriteLine($"{t.FullName} [{(t.IsEnum ? "enum" : t.IsValueType ? "struct" : "class")}]");
            foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Console.WriteLine($"  - {p.PropertyType.Name} {p.Name}");
            foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Where(m => !m.IsSpecialName))
                Console.WriteLine($"  + {m.ReturnType.Name} {m.Name}(...)");
        }
        Console.ReadKey();
    }
}
