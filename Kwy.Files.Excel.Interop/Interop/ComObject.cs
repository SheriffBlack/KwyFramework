using System.Runtime.InteropServices;

namespace Kwy.Files.Excel.Interop.Interop;

internal static class ComObject
{
    public static void Release(object? comObject)
    {
        if (comObject == null)
        {
            return;
        }

        try
        {
            if (Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }
        catch
        {
        }
    }
}
