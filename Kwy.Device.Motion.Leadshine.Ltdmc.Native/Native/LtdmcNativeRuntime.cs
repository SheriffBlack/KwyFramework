using System.Runtime.InteropServices;

namespace Kwy.Device.Motion.Leadshine.Ltdmc.Native;

/// <summary>检查当前 LTDMC 原生库与进程位数是否匹配。</summary>
internal static class LtdmcNativeRuntime
{
    public static void EnsureCompatibleProcess()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
        {
            return;
        }

        throw new PlatformNotSupportedException(
            "The bundled Leadshine LTDMC.dll is x86. Publish and run the application as win-x86, " +
            "or replace it with a verified official x64 LTDMC SDK package before using a 64-bit process.");
    }
}
