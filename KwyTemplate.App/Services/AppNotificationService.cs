using System.Windows;
using System.Windows.Threading;
using Kwy.UI.WPF.Components;
using Kwy.UI.WPF.Components.ActivityLog;
using Kwy.UI.WPF.Components.Dialogs.Message;
using Microsoft.Extensions.Logging;

namespace KwyTemplate.App.Services;

public sealed class AppNotificationService : IAppNotificationService
{
    private readonly IDialogMessageService dialogMessageService;
    private readonly KwyActivityLogService activityLog;
    private readonly ILogger<AppNotificationService> developerLog;
    private readonly Dispatcher dispatcher;

    public AppNotificationService(
        IDialogMessageService dialogMessageService,
        KwyActivityLogService activityLog,
        ILogger<AppNotificationService> developerLog)
    {
        this.dialogMessageService = dialogMessageService ?? throw new ArgumentNullException(nameof(dialogMessageService));
        this.activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        this.developerLog = developerLog ?? throw new ArgumentNullException(nameof(developerLog));
        dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public async Task<bool> ConfirmAsync(string message, string? title = null, bool writeLog = false)
    {
        if (writeLog)
        {
            activityLog.Info(FormatLogMessage(title, message));
        }

        return await RunOnUiAsync(() => dialogMessageService.ShowConfirmAsync(message, title)).ConfigureAwait(false);
    }

    public async Task InfoAsync(string message, string? title = null, bool writeLog = true)
    {
        if (writeLog)
        {
            activityLog.Info(FormatLogMessage(title, message));
        }

        await RunOnUiAsync(() => dialogMessageService.ShowInfoAsync(message, title)).ConfigureAwait(false);
    }

    public async Task SuccessAsync(string message, string? title = null, bool writeLog = true)
    {
        if (writeLog)
        {
            activityLog.Success(FormatLogMessage(title, message));
        }

        await RunOnUiAsync(() => dialogMessageService.ShowMessageAsync(message, DialogMessageIcon.Success, title)).ConfigureAwait(false);
    }

    public async Task WarningAsync(string message, string? title = null, bool writeLog = true)
    {
        if (writeLog)
        {
            activityLog.Warn(FormatLogMessage(title, message));
        }

        await RunOnUiAsync(() => dialogMessageService.ShowWarningAsync(message, title)).ConfigureAwait(false);
    }

    public async Task ErrorAsync(string message, string? title = null, Exception? exception = null, bool writeLog = true)
    {
        string fullMessage = exception == null ? message : $"{message}\n{exception.Message}";
        if (exception != null)
        {
            developerLog.LogError(
                exception,
                "Application notification error: {Message}",
                message);
        }

        if (writeLog)
        {
            activityLog.Error(FormatLogMessage(title, fullMessage));
        }

        await RunOnUiAsync(() => dialogMessageService.ShowErrorAsync(fullMessage, title)).ConfigureAwait(false);
    }

    private async Task RunOnUiAsync(Func<Task> action)
    {
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            await action().ConfigureAwait(true);
            return;
        }

        await dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task.Unwrap().ConfigureAwait(false);
    }

    private async Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
    {
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return default!;
        }

        if (dispatcher.CheckAccess())
        {
            return await action().ConfigureAwait(true);
        }

        return await dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task.Unwrap().ConfigureAwait(false);
    }

    private static string FormatLogMessage(string? title, string message)
        => string.IsNullOrWhiteSpace(title) ? message : $"[{title}] {message}";
}

