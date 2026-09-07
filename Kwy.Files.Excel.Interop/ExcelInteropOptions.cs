namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropOptions
{
    public bool Visible { get; set; }

    public bool DisplayAlerts { get; set; }

    public bool ScreenUpdating { get; set; }

    public int MaxComRetries { get; set; } = 20;

    public TimeSpan ComRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public bool KillOwnedExcelProcessOnDispose { get; set; }
}
