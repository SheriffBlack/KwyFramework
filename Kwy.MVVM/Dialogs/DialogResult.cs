namespace Kwy.MVVM.Dialogs;

/// <summary>
/// 对话框关闭时的用户交互结果。
/// </summary>
public enum ButtonResult
{
    None, OK, Cancel, Abort, Retry, Ignore, Yes, No
}

/// <summary>
/// 对话框关闭结果接口。
/// </summary>
public interface IDialogResult
{
    IDialogParameters Parameters { get; }
    ButtonResult Result { get; }
}

/// <summary>
/// 对话框结果。使用 record 提供不可变性和极简语法。
/// </summary>
public record DialogResult(ButtonResult Result, IDialogParameters Parameters) : IDialogResult
{
    /// <summary>
    /// 提供只传入 Result 的快捷构造函数，内部自动初始化一个空的 Parameters。
    /// </summary>
    public DialogResult(ButtonResult result) : this(result, new DialogParameters())
    {
    }
}