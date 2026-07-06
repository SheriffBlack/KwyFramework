namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// Options for saving a file.
/// </summary>
public sealed class SaveFileDialogOptions : FileDialogOptions
{
    /// <summary>
    /// Gets or sets whether an extension is automatically added.
    /// </summary>
    public bool AddExtension { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an overwrite prompt is shown when the file exists.
    /// </summary>
    public bool OverwritePrompt { get; set; } = true;
}
