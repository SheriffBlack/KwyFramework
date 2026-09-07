namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Feature flags advertised by an Excel provider.
/// </summary>
[Flags]
public enum ExcelProviderFeatures
{
    None = 0,
    ReadWorkbook = 1 << 0,
    WriteWorkbook = 1 << 1,
    Xls = 1 << 2,
    Xlsx = 1 << 3,
    Csv = 1 << 4,
    TemplateCopy = 1 << 5,
    PreserveStyles = 1 << 6,
    PreservePictures = 1 << 7,
    PreserveMergedCells = 1 << 8,
    Formula = 1 << 9,
    InteropAutomation = 1 << 10,
    EncryptedTransparentRead = 1 << 11
}
