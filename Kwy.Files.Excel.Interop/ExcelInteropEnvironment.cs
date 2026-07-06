using System.Runtime.InteropServices;
using OfficeExcel = global::Microsoft.Office.Interop.Excel;

namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropEnvironment : IExcelInteropEnvironment
{
    public bool IsExcelInstalled()
    {
        OfficeExcel.Application? application = null;
        try
        {
            application = new OfficeExcel.Application();
            _ = application.Version;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (application != null)
            {
                try
                {
                    application.Quit();
                }
                catch
                {
                }

                if (Marshal.IsComObject(application))
                {
                    Marshal.FinalReleaseComObject(application);
                }
            }
        }
    }

    public void EnsureExcelInstalled()
    {
        if (!IsExcelInstalled())
        {
            throw new ExcelInteropException(
                "Microsoft Excel is not installed or cannot be started. Kwy.Files.Excel.Interop requires a local Microsoft Excel installation.");
        }
    }
}
