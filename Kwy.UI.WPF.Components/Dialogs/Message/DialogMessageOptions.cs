namespace Kwy.UI.WPF.Components.Dialogs;

/// <summary>
/// 标准消息对话框的显示选项。
/// </summary>
public sealed class DialogMessageOptions
{
    public string? Title { get; init; }

    public DialogMessageIcon Icon { get; init; }

    public bool ShowCancelButton { get; init; }

    public string? ConfirmButtonText { get; init; }

    public string? CancelButtonText { get; init; }
}
