namespace Kwy.Files.Excel.Interop;

public sealed class ExcelInteropException : InvalidOperationException
{
    public ExcelInteropException(string message)
        : base(message)
    {
    }

    public ExcelInteropException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
