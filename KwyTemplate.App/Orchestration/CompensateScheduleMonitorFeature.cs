using System.Globalization;
using Kwy.MVVM.Messaging;
using KwyTemplate.App.Messages;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// Monitors the configured A/B shift check windows. Completion remains the PLC
/// point-check flag; this feature intentionally keeps no completion file.
/// </summary>
public sealed class CompensateScheduleMonitorFeature : IMachineRuntimeFeature
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private readonly CompensateOptionsStore optionsStore;
    private readonly IAppNotificationService notificationService;
    private readonly ILocalizationService localizationService;
    private readonly IDisposable optionsChangedSubscription;
    private readonly object syncRoot = new();
    private MachineBase? machine;
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private string? activeWindowKey;
    private string? latestObservedExpiredWindowKey;
    private bool initialized;
    private bool configurationChanged;
    private bool disposed;

    public CompensateScheduleMonitorFeature(
        CompensateOptionsStore optionsStore,
        IAppNotificationService notificationService,
        ILocalizationService localizationService,
        IMessageBus messageBus)
    {
        this.optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(messageBus);
        optionsChangedSubscription = messageBus.Subscribe<CompensateScheduleMonitorFeature, CompensateOptionsChangedMessage>(
            this,
            static (feature, _) => feature.OnOptionsChanged());
    }

    public bool CanAttach(MachineBase machine) => true;

    public void Start(MachineBase machine)
    {
        if (disposed)
        {
            return;
        }

        lock (syncRoot)
        {
            if (worker is { IsCompleted: false })
            {
                return;
            }

            this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
            initialized = false;
            configurationChanged = false;
            activeWindowKey = null;
            latestObservedExpiredWindowKey = null;
            stopCts = new CancellationTokenSource();
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
            machine = null;
            activeWindowKey = null;
            initialized = false;
            configurationChanged = false;
            latestObservedExpiredWindowKey = null;
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
        optionsChangedSubscription.Dispose();
        Stop();
    }

    private void OnOptionsChanged()
    {
        lock (syncRoot)
        {
            configurationChanged = true;
        }
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAsync(DateTimeOffset.Now, cancellationToken).ConfigureAwait(false);
                await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // A failed PLC read or UI notification must not stop later shift checks.
                await DelaySafelyAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task EvaluateAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        MachineBase? currentMachine;
        bool shouldResetForConfiguration;
        lock (syncRoot)
        {
            currentMachine = machine;
            shouldResetForConfiguration = configurationChanged;
            configurationChanged = false;
        }

        if (currentMachine == null)
        {
            return;
        }

        CompensateOptions options = optionsStore.Current;
        if (!options.IsEnabled || !TryCreateSchedule(now, options, out IReadOnlyList<CheckWindow> windows))
        {
            ResetTransientState();
            return;
        }

        CheckWindow? activeWindow = windows.FirstOrDefault(window => window.Contains(now));
        CheckWindow? latestExpiredWindow = windows
            .Where(window => window.End <= now)
            .OrderByDescending(window => window.End)
            .FirstOrDefault();

        if (!initialized || shouldResetForConfiguration)
        {
            // At application start trust the current PLC flag and establish the
            // latest expired window as a baseline. A PC restart must not replay a
            // reminder for a window that ended before this process was running.
            initialized = true;
            activeWindowKey = activeWindow?.Key;
            latestObservedExpiredWindowKey = latestExpiredWindow?.Key;
            return;
        }
        else if (!string.Equals(activeWindowKey, activeWindow?.Key, StringComparison.Ordinal))
        {
            activeWindowKey = activeWindow?.Key;
        }

        if (latestExpiredWindow == null
            || string.Equals(latestObservedExpiredWindowKey, latestExpiredWindow.Key, StringComparison.Ordinal))
        {
            return;
        }

        latestObservedExpiredWindowKey = latestExpiredWindow.Key;

        bool? isCompleted = await currentMachine.ReadCheckCompletedAsync(cancellationToken).ConfigureAwait(false);
        if (isCompleted is not false)
        {
            return;
        }

        // 逾期仍未完成时，明确保持 PLC 为未完成状态，避免 PLC 端残留状态
        // 与本次排程判定不一致。
        await currentMachine.SetCheckCompletedAsync(false, cancellationToken).ConfigureAwait(false);

        string start = latestExpiredWindow.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
        string end = latestExpiredWindow.End.ToString("HH:mm", CultureInfo.InvariantCulture);
        await notificationService.WarningAsync(
            localizationService.TF(
                "Compensate.Message.ScheduleCheckRequired",
                "未在规定时间{0}~{1}内点检，请完成！",
                start,
                end),
            localizationService.T("Compensate.Title.CheckReminder", "点检提醒")).ConfigureAwait(false);
    }

    private void ResetTransientState()
    {
        lock (syncRoot)
        {
            initialized = false;
            activeWindowKey = null;
            latestObservedExpiredWindowKey = null;
        }
    }

    private static bool TryCreateSchedule(DateTimeOffset now, CompensateOptions options, out IReadOnlyList<CheckWindow> windows)
    {
        windows = [];
        if (options.CheckWindow <= 0
            || !TryParseHour(options.CompensateATime1, out int aTime1)
            || !TryParseHour(options.CompensateATime2, out int aTime2)
            || !TryParseHour(options.CompensateBTime1, out int bTime1)
            || !TryParseHour(options.CompensateBTime2, out int bTime2))
        {
            return false;
        }

        int[] scheduledHours = [aTime1, aTime2, bTime1, bTime2];
        DateTimeOffset day = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        windows = Enumerable.Range(-1, 3)
            .SelectMany(dayOffset => scheduledHours.Select(hour => CreateWindow(day.AddDays(dayOffset), hour, options.CheckWindow)))
            .OrderBy(window => window.Start)
            .ToArray();
        return true;
    }

    private static CheckWindow CreateWindow(DateTimeOffset day, int hour, double durationHours)
    {
        DateTimeOffset start = day.AddHours(hour);
        return new CheckWindow(
            $"{start:yyyyMMdd-HH}",
            start,
            start.AddHours(durationHours));
    }

    private static bool TryParseHour(string? value, out int hour)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out hour)
            && hour is >= 0 and <= 23;

    private static async Task DelaySafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed record CheckWindow(string Key, DateTimeOffset Start, DateTimeOffset End)
    {
        public bool Contains(DateTimeOffset now) => Start <= now && now < End;
    }
}
