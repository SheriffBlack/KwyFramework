namespace Kwy.UI.Services.FileDialogs;

/// <summary>
/// 打开一个或多个文件时使用的选项。
/// </summary>
public sealed class OpenFileDialogOptions : FileDialogOptions
{
    /// <summary>
    /// 获取或设置是否允许多选文件。
    /// </summary>
    public bool Multiselect { get; set; }

    /// <summary>
    /// 获取或设置所选文件是否必须存在。
    /// </summary>
    public bool CheckFileExists { get; set; } = true;
}
