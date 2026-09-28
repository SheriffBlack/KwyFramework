namespace Kwy.Files.Excel.Abstractions.Models;

/// <summary>
/// Describes an Excel provider implementation.
/// </summary>
public sealed record ExcelProviderInfo(
    string Name,
    ExcelProviderFeatures Features,
    IReadOnlySet<ExcelFileFormat> SupportedFormats);
