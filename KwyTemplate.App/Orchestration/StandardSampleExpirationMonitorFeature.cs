using System.Globalization;
using KwyTemplate.App.Models;
using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// 标准件有效期午夜检查 Feature。
/// 每次检查后都重新计算下一次本地 00:00，避免长期运行时按 24 小时累加造成定时漂移。
/// </summary>
public sealed class StandardSampleExpirationMonitorFeature : IMachineRuntimeFeature
{
    private readonly StandardSampleState sampleState;
    private readonly object syncRoot = new();
    private MachineBase? machine;
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private bool disposed;

    public StandardSampleExpirationMonitorFeature(StandardSampleState sampleState)
    {
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
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
            TimeSpan delay = GetDelayToNextMidnight(DateTimeOffset.Now);
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
                // 午夜检查失败不结束 Feature；下一轮 00:00 重新检查。
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

        string expireDate = sampleState.StandardSample.ExpireDate;
        if (string.IsNullOrWhiteSpace(expireDate) || !TryParseMesDateTime(expireDate, out DateTime expireTime))
        {
            return;
        }

        if (expireTime < DateTime.Now)
        {
            await currentMachine.SetStandardSampleExpiredAsync(true, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TimeSpan GetDelayToNextMidnight(DateTimeOffset now)
    {
        DateTimeOffset nextMidnight = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        if (nextMidnight <= now)
        {
            nextMidnight = nextMidnight.AddDays(1);
        }

        return nextMidnight - now;
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