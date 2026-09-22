namespace Kwy.UI.Services.FileDialogs;

/// <summary>
/// 选择文件夹时使用的选项。
/// </summary>
public sealed class FolderDialogOptions
{
    /// <summary>
    /// 获取或设置对话框标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置初始目录。目录无效时，服务回退到最近一次选择的目录或桌面。
    /// </summary>
    public string? InitialDirectory { get; set; }
}
