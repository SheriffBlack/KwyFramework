namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// Options for opening one or more files.
/// </summary>
public sealed class OpenFileDialogOptions : FileDialogOptions
{
    /// <summary>
    /// Gets or sets whether multiple files can be selected.
    /// </summary>
    public bool Multiselect { get; set; }

    /// <summary>
    /// Gets or sets whether the selected file must exist.
    /// </summary>
    public bool CheckFileExists { get; set; } = true;
}
