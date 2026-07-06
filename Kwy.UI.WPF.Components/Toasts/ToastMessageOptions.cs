namespace Kwy.UI.WPF.Components.Toasts;

public sealed class ToastMessageOptions
{
    public string Token { get; set; } = ToastTokens.Root;

    public TimeSpan? Duration { get; set; }

    public DialogMessageIcon Icon { get; set; } = DialogMessageIcon.None;
}
