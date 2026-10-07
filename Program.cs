using System;
using System.Reflection;
class Program {
    static void Main() {
        var asm = Assembly.LoadFrom(@"C:\Users\Administrator\Desktop\SCPSL pulint\zeropl\ex\Exiled.API.dll");
        var t = asm.GetType("Exiled.API.Features.BanHandler");
        if (t == null) { Console.WriteLine("BanHandler not found"); return; }
        Console.WriteLine("Methods:");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            Console.WriteLine("  " + m.Name);
    }
}
