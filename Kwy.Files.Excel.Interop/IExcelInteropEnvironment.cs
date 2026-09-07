namespace Kwy.Files.Excel.Interop;

public interface IExcelInteropEnvironment
{
    bool IsExcelInstalled();

    void EnsureExcelInstalled();
}
