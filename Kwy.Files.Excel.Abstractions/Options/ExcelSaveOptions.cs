namespace Kwy.Files.Excel.Abstractions;

public sealed class ExcelSaveOptions
{
    public string FilePath { get; set; } = string.Empty;

    public ExcelFileFormat Format { get; set; } = ExcelFileFormat.Auto;

    public bool Overwrite { get; set; } = true;

    public string? Password { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(FilePath));
        }
    }
}
