namespace Kwy.UI.WPF.Components.Dialogs;

/// <summary>
/// 输入对话框服务。
/// </summary>
public interface IInputDialogService
{
    /// <summary>
    /// 显示输入对话框。
    /// </summary>
    Task<InputDialogResult> ShowAsync(InputDialogOptions options);

    /// <summary>
    /// 显示文本输入对话框。
    /// </summary>
    Task<InputDialogResult> ShowTextAsync(string message, string? title = null, string? defaultValue = null);

    /// <summary>
    /// 显示数值输入对话框。
    /// </summary>
    Task<InputDialogResult> ShowNumberAsync(
        string message,
        string? title = null,
        decimal? defaultValue = null,
        decimal? minimum = null,
        decimal? maximum = null,
        string? unit = null);
}
