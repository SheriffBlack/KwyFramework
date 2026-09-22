namespace Kwy.UI.Services.FileDialogs;

/// <summary>
/// 文件对话框的基础选项。
/// </summary>
public abstract class FileDialogOptions
{
    /// <summary>
    /// 获取或设置对话框标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 获取或设置文件筛选条件，例如“JSON (*.json)|*.json|所有文件 (*.*)|*.*”。
    /// <see langword="null"/> 或空值表示不设置库预定义的筛选条件。
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// 获取或设置初始目录。目录无效时，服务回退到最近一次选择的目录或桌面。
    /// </summary>
    public string? InitialDirectory { get; set; }

    /// <summary>
    /// 获取或设置默认文件名。
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 获取或设置默认扩展名。为空时从 <see cref="Filter"/> 推断。
    /// </summary>
    public string? DefaultExtension { get; set; }
}
