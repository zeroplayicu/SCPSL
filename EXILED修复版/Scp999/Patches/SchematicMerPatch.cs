using System.Diagnostics;
using Exiled.API.Features;
using HarmonyLib;

namespace Scp999.Patches;

[HarmonyPatch(typeof(ProjectMER.ProjectMER), nameof(ProjectMER.ProjectMER.SchematicsDir), MethodType.Getter)]
public class SchematicMerPatch
{
    public static bool Prefix(ref string __result)
    {
        var stackTrace = new StackTrace();
        foreach (var frame in stackTrace.GetFrames())
        {
            var declaringType = frame.GetMethod()?.DeclaringType;

            // 本轮修复: 动态方法/编译器生成方法等帧的 DeclaringType 可能为 null，
            // 原实现直接访问 .Assembly 会抛 NRE 并破坏被 patch 的 getter
            if (declaringType == null)
                continue;

            var assemblyName = declaringType.Assembly.GetName().Name;

            if (assemblyName == "Scp999" && declaringType.Name == "SchematicManager")
            {
                __result = Plugin.Singleton.SchematicPath;
                return false;
            }
        }

        return true;
    }
}