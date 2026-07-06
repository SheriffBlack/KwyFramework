namespace Kwy.Files.Excel.Abstractions;

public sealed class ExcelOpenOptions
{
    public string FilePath { get; set; } = string.Empty;

    public ExcelFileFormat Format { get; set; } = ExcelFileFormat.Auto;

    public bool ReadOnly { get; set; } = true;

    public string? Password { get; set; }

    /// <summary>
    /// Allows providers to use enterprise transparent encryption behavior when supported.
    /// </summary>
    public bool AllowTransparentEncryptedRead { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(FilePath));
        }
    }
}
