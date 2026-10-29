using System.Globalization;
using System.Windows;
using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// 标准件/确认件有效期整点检查 Feature。
/// 每次检查后都重新计算下一个本地整点，避免按固定间隔累加产生漂移。
/// </summary>
public sealed class StandardSampleExpirationMonitorFeature : IMachineRuntimeFeature
{
    private readonly StandardSampleState sampleState;
    private readonly IAppNotificationService notificationService;
    private readonly ILocalizationService localizationService;
    private readonly object syncRoot = new();
    private MachineBase? machine;
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private bool disposed;

    public StandardSampleExpirationMonitorFeature(
        StandardSampleState sampleState,
        IAppNotificationService notificationService,
        ILocalizationService localizationService)
    {
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
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
        while (!cancellationToken.IsCancellationRequested)
        {
            TimeSpan delay = GetDelayToNextHour(DateTimeOffset.Now);
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                await CheckStandardSampleExpirationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // 整点检查失败不结束 Feature；下一轮整点重新检查。
            }
        }
    }

    private async Task CheckStandardSampleExpirationAsync(CancellationToken cancellationToken)
    {
        MachineBase? currentMachine;
        lock (syncRoot)
        {
            currentMachine = machine;
        }

        if (currentMachine == null)
        {
            return;
        }

        DateTime now = DateTime.Now;
        bool standardSampleExpired = IsExpired(sampleState.StandardSample.ExpireDate, now);
        bool confirmSampleExpired = IsExpired(sampleState.ConfirmSample.ExpireDate, now);
        if (standardSampleExpired || confirmSampleExpired)
        {
            await currentMachine.SetStandardSampleExpiredAsync(true, cancellationToken).ConfigureAwait(false);
            string expiredSampleNames = GetExpiredSampleNames(standardSampleExpired, confirmSampleExpired);
            await RunOnUiAsync(sampleState.ClearAll).ConfigureAwait(false);
            await notificationService.WarningAsync(
                localizationService.TF(
                    "Standard.Message.SampleExpired",
                    "{0}已过期，已清空标准件与确认件数据。",
                    expiredSampleNames),
                localizationService.T("Standard.Title.SampleExpired", "标准件/确认件已过期"))
                .ConfigureAwait(false);
        }
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private static bool IsExpired(string expireDate, DateTime now)
        => !string.IsNullOrWhiteSpace(expireDate)
            && TryParseMesDateTime(expireDate, out DateTime expireTime)
            && expireTime <= now;

    private string GetExpiredSampleNames(bool standardSampleExpired, bool confirmSampleExpired)
    {
        var names = new List<string>(2);
        if (standardSampleExpired)
        {
            names.Add(localizationService.T("Standard.Header.StandardSample", "标准件"));
        }

        if (confirmSampleExpired)
        {
            names.Add(localizationService.T("Standard.Header.ConfirmSample", "确认件"));
        }

        return string.Join("、", names);
    }

    private static TimeSpan GetDelayToNextHour(DateTimeOffset now)
    {
        DateTimeOffset nextHour = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            0,
            0,
            now.Offset).AddHours(1);
        if (nextHour <= now)
        {
            nextHour = nextHour.AddHours(1);
        }

        return nextHour - now;
    }

    private static bool TryParseMesDateTime(string value, out DateTime dateTime)
    {
        string[] formats =
        [
            "yyyy/M/d H:m:s",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/M/d HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss"
        ];

        return DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out dateTime)
            || DateTime.TryParse(
                value,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out dateTime)
            || DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out dateTime);
    }
}
