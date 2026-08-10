using Kwy.MVVM.Dialogs;

namespace Kwy.UI.WPF.Components.Dialogs;

/// <summary>
/// 提供标准消息对话框。
/// </summary>
public interface IDialogMessageService
{
    /// <summary>
    /// 显示消息并返回完整按钮结果。
    /// </summary>
    Task<ButtonResult> ShowAsync(string message, DialogMessageOptions? options = null);

    /// <summary>
    /// 显示通用消息对话框。确定返回 true，取消或关闭返回 false。
    /// </summary>
    Task<bool> ShowMessageAsync(string message, DialogMessageIcon icon = DialogMessageIcon.None, string? title = null);

    Task<bool> ShowConfirmAsync(string message, string? title = null);

    Task<bool> ShowWarningAsync(string message, string? title = null);

    Task<bool> ShowErrorAsync(string message, string? title = null);

    Task<bool> ShowInfoAsync(string message, string? title = null);
}
