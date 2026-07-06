namespace Kwy.Files.Excel.Abstractions;

public sealed class ExcelTemplateCopyOptions
{
    public string TemplateFilePath { get; set; } = string.Empty;

    public string TemplateSheetName { get; set; } = string.Empty;

    public string TargetFilePath { get; set; } = string.Empty;

    public string? NewSheetName { get; set; }

    public bool ReplaceIfExists { get; set; } = true;

    public bool PreserveStyles { get; set; } = true;

    public bool PreservePictures { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TemplateFilePath))
        {
            throw new ArgumentException("Template file path cannot be empty.", nameof(TemplateFilePath));
        }

        if (string.IsNullOrWhiteSpace(TemplateSheetName))
        {
            throw new ArgumentException("Template sheet name cannot be empty.", nameof(TemplateSheetName));
        }

        if (string.IsNullOrWhiteSpace(TargetFilePath))
        {
            throw new ArgumentException("Target file path cannot be empty.", nameof(TargetFilePath));
        }
    }
}
