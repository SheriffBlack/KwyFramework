namespace Kwy.MVVM.Dialogs;

/// <summary>
/// 弹窗的 ViewModel 需实现此接口，以便接收弹窗参数并控制窗口生命周期。
/// 对应 Prism 中的 `IDialogAware`。
/// </summary>
public interface IDialogAware
{
    /// <summary>
    /// 绑定到窗口的对话框标题。
    /// </summary>
    string Title { get; }

    /// <summary>
    /// 请求关闭该对话框的事件通知。传入结果作为参数并触发给调用的发起者。
    /// 在 ViewModel 中通过 `RequestClose?.Invoke(new DialogResult(ButtonResult.OK))` 触发关闭。
    /// </summary>
    event Action<IDialogResult> RequestClose;

    /// <summary>
    /// 决定当前对话框能否被关闭 (例如验证未通过时返回 false 阻止右上角X被点下)。
    /// </summary>
    bool CanCloseDialog();

    /// <summary>
    /// 当对话框被成功关闭时触发。
    /// 可以用来清理资源、反注册事件等。
    /// </summary>
    void OnDialogClosed();

    /// <summary>
    /// 当对话框被打开时触发，用于接收外部传入的参数。
    /// </summary>
    void OnDialogOpened(IDialogParameters parameters);
}