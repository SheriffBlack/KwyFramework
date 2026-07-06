namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// Base options shared by WPF file dialogs.
/// </summary>
public abstract class FileDialogOptions
{
    /// <summary>
    /// Gets or sets the dialog title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the file filter, such as "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*".
    /// </summary>
    public string Filter { get; set; } = "所有文件 (*.*)|*.*";

    /// <summary>
    /// Gets or sets the initial directory. If invalid, the service falls back to the last selected directory or Desktop.
    /// </summary>
    public string? InitialDirectory { get; set; }

    /// <summary>
    /// Gets or sets the default file name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the default extension. If empty, it is inferred from <see cref="Filter"/>.
    /// </summary>
    public string? DefaultExtension { get; set; }
}
