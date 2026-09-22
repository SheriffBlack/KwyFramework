namespace Kwy.UI.Services.FileDialogs;

/// <summary>
/// 保存文件时使用的选项。
/// </summary>
public sealed class SaveFileDialogOptions : FileDialogOptions
{
    /// <summary>
    /// 获取或设置是否自动添加扩展名。
    /// </summary>
    public bool AddExtension { get; set; } = true;

    /// <summary>
    /// 获取或设置文件已存在时是否显示覆盖确认提示。
    /// </summary>
    public bool OverwritePrompt { get; set; } = true;
}
