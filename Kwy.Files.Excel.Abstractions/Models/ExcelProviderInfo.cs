namespace Kwy.Files.Excel.Abstractions;

/// <summary>
/// Describes an Excel provider implementation.
/// </summary>
public sealed record ExcelProviderInfo(
    string Name,
    ExcelProviderFeatures Features,
    IReadOnlySet<ExcelFileFormat> SupportedFormats);
