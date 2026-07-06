namespace Kwy.UI.WPF.Services.FileDialogs;

/// <summary>
/// Provides WPF file and folder dialog operations.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Opens a file selection dialog and returns the selected file path.
    /// </summary>
    string? OpenFile(OpenFileDialogOptions options);

    /// <summary>
    /// Opens a file selection dialog and returns all selected file paths.
    /// </summary>
    IReadOnlyList<string> OpenFiles(OpenFileDialogOptions options);

    /// <summary>
    /// Opens a save file dialog and returns the selected file path.
    /// </summary>
    string? SaveFile(SaveFileDialogOptions options);

    /// <summary>
    /// Opens a folder selection dialog and returns the selected folder path.
    /// </summary>
    string? SelectFolder(FolderDialogOptions options);
}
