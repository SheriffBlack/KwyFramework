using Kwy.MVVM.Messaging;
using KwyTemplate.App.Services;
using KwyTemplate.App.Messages;
using KwyTemplate.App.Models;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using System.Globalization;
using System.Windows;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// 自动点检时间窗口监控 Feature。
/// 定时器只负责唤醒检查，窗口判断每次都基于当前系统时间重新计算，避免 7x24 小时运行时产生累计偏移。
/// </summary>
public sealed class CompensateScheduleMonitorFeature : IMachineRuntimeFeature
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private readonly CompensateOptionsStore optionsStore;
    private readonly IMessageBus messageBus;
    private readonly IAppNotificationService notificationService;
    private readonly Dictionary<string, CompensateScheduleWindowState> windowStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object syncRoot = new();
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private IDisposable? workflowCompletedSubscription;
    private IDisposable? optionsChangedSubscription;
    private MachineBase? attachedMachine;
    private DateTimeOffset monitorStartedAt;
    private bool disposed;

    public CompensateScheduleMonitorFeature(
        CompensateOptionsStore optionsStore,
        IMessageBus messageBus,
        IAppNotificationService notificationService)
    {
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    public bool CanAttach(MachineBase machine)
        => machine.TestStations.Any(static station => station.Operations.Any(static operation =>
            string.Equals(operation.Code, StationOperationDescriptor.Check, StringComparison.OrdinalIgnoreCase)));

    public void Start(MachineBase machine)
    {
        if (disposed || !CanAttach(machine))
        {
            return;
        }

        lock (syncRoot)
        {
            if (worker is { IsCompleted: false })
            {
                return;
            }

            attachedMachine = machine;
            monitorStartedAt = DateTimeOffset.Now;
            windowStates.Clear();
            optionsStore.LoadOrCreate();
            stopCts = new CancellationTokenSource();
            workflowCompletedSubscription = messageBus.Subscribe<CompensateScheduleMonitorFeature, CompensateWorkflowCompletedMessage>(
                this,
                static (feature, message) => feature.OnWorkflowCompleted(message),
                MessageSubscribeOptions<CompensateWorkflowCompletedMessage>.OnBackground);
            optionsChangedSubscription = messageBus.Subscribe<CompensateScheduleMonitorFeature, CompensateOptionsChangedMessage>(
                this,
                static (feature, message) => feature.OnOptionsChanged(message),
                MessageSubscribeOptions<CompensateOptionsChangedMessage>.OnBackground);
            worker = MonitorLoopAsync(stopCts.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? runningWorker;

        lock (syncRoot)
        {
            cts = stopCts;
            runningWorker = worker;
            stopCts = null;
            worker = null;
            workflowCompletedSubscription?.Dispose();
            workflowCompletedSubscription = null;
            optionsChangedSubscription?.Dispose();
            optionsChangedSubscription = null;
            attachedMachine = null;
            windowStates.Clear();
        }

        if (cts == null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            runningWorker?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is OperationCanceledException))
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        await CheckWindowsAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(CheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await CheckWindowsAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CheckWindowsAsync(CancellationToken cancellationToken)
    {
        CompensateOptions options = optionsStore.Current;
        if (!options.IsEnabled)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        foreach (CompensateScheduleWindow window in CreateCandidateWindows(options, now))
        {
            cancellationToken.ThrowIfCancellationRequested();

            CompensateScheduleWindowState state = GetOrCreateState(window);
            if (state.End <= monitorStartedAt)
            {
                state.WarningShown = true;
                continue;
            }

            if (now <= state.End || state.IsCompleted || state.WarningShown)
            {
                continue;
            }

            state.WarningShown = true;
            await NotifyMachineScheduleExpiredAsync(state.Start, state.End, cancellationToken).ConfigureAwait(false);
            string message = $"未在规定时间{FormatHour(state.Start)}~{FormatHour(state.End)}内点检，请完成！";
            await ShowWarningOnUiAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task NotifyMachineScheduleExpiredAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        MachineBase? machine = attachedMachine;
        if (machine == null)
        {
            return;
        }

        try
        {
            await machine.OnCompensateScheduleExpiredAsync(start, end, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Do not block the reminder popup when an optional machine-specific action fails.
        }
    }
    private void OnWorkflowCompleted(CompensateWorkflowCompletedMessage message)
    {
        CompensateOptions options = optionsStore.Current;
        if (!options.IsEnabled)
        {
            return;
        }

        foreach (CompensateScheduleWindow window in CreateCandidateWindows(options, message.CompletedAt))
        {
            if (message.CompletedAt < window.Start || message.CompletedAt > window.End)
            {
                continue;
            }

            CompensateScheduleWindowState state = GetOrCreateState(window);
            state.IsCompleted = true;
        }
    }

    private void OnOptionsChanged(CompensateOptionsChangedMessage message)
    {
        lock (syncRoot)
        {
            windowStates.Clear();
            monitorStartedAt = DateTimeOffset.Now;
        }
    }

    private CompensateScheduleWindowState GetOrCreateState(CompensateScheduleWindow window)
    {
        lock (syncRoot)
        {
            if (!windowStates.TryGetValue(window.Key, out CompensateScheduleWindowState? state))
            {
                state = new CompensateScheduleWindowState(window.Key, window.Start, window.End);
                windowStates[window.Key] = state;
            }

            return state;
        }
    }

    private static IEnumerable<CompensateScheduleWindow> CreateCandidateWindows(CompensateOptions options, DateTimeOffset now)
    {
        DateOnly today = DateOnly.FromDateTime(now.DateTime);
        TimeSpan windowDuration = GetCheckWindowDuration(options);
        foreach (DateOnly date in new[] { today.AddDays(-1), today, today.AddDays(1) })
        {
            foreach ((string code, string hourText) in GetConfiguredHours(options))
            {
                if (!TryParseHour(hourText, out int hour))
                {
                    continue;
                }

                DateTimeOffset start = new(date.ToDateTime(TimeOnly.MinValue).AddHours(hour), now.Offset);
                DateTimeOffset end = start.Add(windowDuration);
                yield return new CompensateScheduleWindow($"{date:yyyyMMdd}-{code}-{hour:00}", start, end);
            }
        }
    }


    private static TimeSpan GetCheckWindowDuration(CompensateOptions options)
    {
        double hours = options.CheckWindow;
        return double.IsFinite(hours) && hours > 0
            ? TimeSpan.FromHours(hours)
            : TimeSpan.FromHours(2);
    }
    private static IEnumerable<(string Code, string HourText)> GetConfiguredHours(CompensateOptions options)
    {
        yield return (nameof(options.CompensateATime1), options.CompensateATime1);
        yield return (nameof(options.CompensateATime2), options.CompensateATime2);
        yield return (nameof(options.CompensateBTime1), options.CompensateBTime1);
        yield return (nameof(options.CompensateBTime2), options.CompensateBTime2);
    }

    private static bool TryParseHour(string? value, out int hour)
    {
        hour = 0;
        return !string.IsNullOrWhiteSpace(value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)
            && hour is >= 0 and <= 23;
    }

    private static string FormatHour(DateTimeOffset time)
        => time.Hour.ToString(CultureInfo.InvariantCulture);

    private async Task ShowWarningOnUiAsync(string message, CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            await notificationService.WarningAsync(message, "点检提醒").ConfigureAwait(true);
            return;
        }

        await dispatcher.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await notificationService.WarningAsync(message, "点检提醒").ConfigureAwait(true);
        });
    }

    private sealed record CompensateScheduleWindow(string Key, DateTimeOffset Start, DateTimeOffset End);

    private sealed class CompensateScheduleWindowState
    {
        public CompensateScheduleWindowState(string key, DateTimeOffset start, DateTimeOffset end)
        {
            Key = key;
            Start = start;
            End = end;
        }

        public string Key { get; }

        public DateTimeOffset Start { get; }

        public DateTimeOffset End { get; }

        public bool IsCompleted { get; set; }

        public bool WarningShown { get; set; }
    }
}




