using System.Windows;
using System.Windows.Threading;
using Kwy.UI.WPF.Components;
using Kwy.UI.WPF.Components.ActivityLog;
using Kwy.UI.WPF.Components.Dialogs.Message;
using KwyTemplate.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace KwyTemplate.App.Services;

public sealed class AppNotificationService : IAppNotificationService
{
    private readonly IDialogMessageService dialogMessageService;
    private readonly KwyActivityLogService activityLog;
    private readonly IOperatorActivitySink operatorActivitySink;
    private readonly ILogger<AppNotificationService> developerLog;
    private readonly Dispatcher dispatcher;

    public AppNotificationService(
        IDialogMessageService dialogMessageService,
        KwyActivityLogService activityLog,
        IOperatorActivitySink operatorActivitySink,
        ILogger<AppNotificationService> developerLog)
    {
        this.dialogMessageService = dialogMessageService ?? throw new ArgumentNullException(nameof(dialogMessageService));
        this.activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        this.operatorActivitySink = operatorActivitySink ?? throw new ArgumentNullException(nameof(operatorActivitySink));
        this.developerLog = developerLog ?? throw new ArgumentNullException(nameof(developerLog));
        dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public async Task<bool> ConfirmAsync(string message, string? title = null, bool writeLog = false)
    {
        if (writeLog)
        {
            activityLog.Info(FormatLogMessage(title, message));
            await WriteOperatorActivityAsync(
                OperatorActivityLevel.Information,
                "APPLICATION.CONFIRMATION",
                title,
                message).ConfigureAwait(false);
        }

        return await RunOnUiAsync(() => dialogMessageService.ShowConfirmAsync(message, title)).ConfigureAwait(false);
    }

    public async Task InfoAsync(string message, string? title = null, bool writeLog = true)
    {
        if (writeLog)
        {
            activityLog.Info(FormatLogMessage(title, message));
            await WriteOperatorActivityAsync(
                OperatorActivityLevel.Information,
                "APPLICATION.INFORMATION",
                title,
                message).ConfigureAwait(false);
        }

        await RunOnUiAsync(() => dialogMessageService.ShowInfoAsync(message, title)).ConfigureAwait(false);
    }

    public async Task SuccessAsync(string message, string? title = null, bool writeLog = true)
    {
        if (writeLog)
        {
            activityLog.Success(FormatLogMessage(title, message));
            await WriteOperatorActivityAsync(
                OperatorActivityLevel.Success,
                "APPLICATION.SUCCESS",
                title,
                message).ConfigureAwait(false);
        }

        await RunOnUiAsync(() => dialogMessageService.ShowMessageAsync(message, DialogMessageIcon.Success, title)).ConfigureAwait(false);
    }

    public async Task WarningAsync(string message, string? title = null, bool writeLog = true)
    {
        if (writeLog)
        {
            activityLog.Warn(FormatLogMessage(title, message));
            await WriteOperatorActivityAsync(
                OperatorActivityLevel.Warning,
                "APPLICATION.WARNING",
                title,
                message).ConfigureAwait(false);
        }

        await RunOnUiAsync(() => dialogMessageService.ShowWarningAsync(message, title)).ConfigureAwait(false);
    }

    public async Task ErrorAsync(string message, string? title = null, Exception? exception = null, bool writeLog = true)
    {
        if (exception != null)
        {
            developerLog.LogError(
                exception,
                "Application notification error: {Message}",
                message);
        }

        if (writeLog)
        {
            activityLog.Error(FormatLogMessage(title, message));
            await WriteOperatorActivityAsync(
                OperatorActivityLevel.Error,
                "APPLICATION.ERROR",
                title,
                message).ConfigureAwait(false);
        }

        await RunOnUiAsync(() => dialogMessageService.ShowErrorAsync(message, title)).ConfigureAwait(false);
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

    private async Task WriteOperatorActivityAsync(
        OperatorActivityLevel level,
        string eventCode,
        string? title,
        string message)
    {
        try
        {
            await operatorActivitySink.WriteAsync(
                new OperatorActivityEntry(
                    DateTimeOffset.Now,
                    level,
                    eventCode,
                    string.IsNullOrWhiteSpace(title) ? "Application" : title,
                    message)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // 操作员日志持久化失败不能阻断原本的界面通知。
            developerLog.LogError(
                exception,
                "Failed to persist operator activity {EventCode}",
                eventCode);
        }
    }
}

