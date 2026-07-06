namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// Options for selecting a folder.
/// </summary>
public sealed class FolderDialogOptions
{
    /// <summary>
    /// Gets or sets the dialog title.
    /// </summary>
    public string Title { get; set; } = "选择文件夹";

    /// <summary>
    /// Gets or sets the initial directory. If invalid, the service falls back to the last selected directory or Desktop.
    /// </summary>
    public string? InitialDirectory { get; set; }
}
