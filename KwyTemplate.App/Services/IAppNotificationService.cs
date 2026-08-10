namespace KwyTemplate.App.Services;

/// <summary>
/// 弹窗二次封装：弹窗 + 推送到LogView界面
/// </summary>
public interface IAppNotificationService
{
    Task<bool> ConfirmAsync(string message, string? title = null, bool writeLog = false);

    Task InfoAsync(string message, string? title = null, bool writeLog = true);

    Task SuccessAsync(string message, string? title = null, bool writeLog = true);

    Task WarningAsync(string message, string? title = null, bool writeLog = true);

    Task ErrorAsync(string message, string? title = null, Exception? exception = null, bool writeLog = true);
}
