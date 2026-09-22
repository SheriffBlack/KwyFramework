namespace Kwy.UI.Services.FileDialogs;

/// <summary>
/// 提供文件与文件夹对话框及资源管理器打开能力。
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// 打开文件选择对话框，并返回所选文件路径。
    /// </summary>
    string? OpenFile(OpenFileDialogOptions options);

    /// <summary>
    /// 打开文件选择对话框，并返回全部所选文件路径。
    /// </summary>
    IReadOnlyList<string> OpenFiles(OpenFileDialogOptions options);

    /// <summary>
    /// 打开保存文件对话框，并返回所选保存路径。
    /// </summary>
    string? SaveFile(SaveFileDialogOptions options);

    /// <summary>
    /// 打开文件夹选择对话框，并返回所选文件夹路径。
    /// </summary>
    string? SelectFolder(FolderDialogOptions options);

    /// <summary>
    /// 在 Windows 资源管理器中打开目录，或在其所在目录中选中文件。
    /// 传入 <see langword="null"/> 或空路径时打开“此电脑”。
    /// </summary>
    /// <param name="path">已有的文件或目录路径；传入 <see langword="null"/> 时打开“此电脑”。</param>
    /// <returns>成功启动资源管理器时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    bool OpenInFileExplorer(string? path = null);
}
