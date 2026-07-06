namespace Kwy.UI.WPF.Components.Toasts;

public interface IToastMessageService
{
    void Show(string message, ToastMessageOptions? options = null);

    void Show(string message, DialogMessageIcon icon, TimeSpan? duration = null, string? token = null);

    void ShowSuccess(string message, TimeSpan? duration = null, string? token = null);

    void ShowInfo(string message, TimeSpan? duration = null, string? token = null);

    void ShowWarning(string message, TimeSpan? duration = null, string? token = null);

    void ShowError(string message, TimeSpan? duration = null, string? token = null);

    void Clear(string? token = null);
}
