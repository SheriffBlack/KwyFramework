using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.Flow;

/// <summary>
/// 工站实时调度器：只负责捕捉触发、读取结果、读取仪表并完成 PLC/IO 握手。
/// UI、统计、保存等非实时工作通过 StationResultDispatchQueue 后台处理。
/// </summary>
public sealed class CompositeDataDeal
{
    private readonly MachineBase machine;
    private readonly List<IStationDataDeal> subDeals;
    private readonly StationResultDispatchQueue? dispatchQueue;

    public CompositeDataDeal(MachineBase machine, List<IStationDataDeal> subDeals)
        : this(machine, subDeals, null)
    {
    }

    internal CompositeDataDeal(MachineBase machine, List<IStationDataDeal> subDeals, StationResultDispatchQueue? dispatchQueue)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.subDeals = subDeals ?? [];
        this.dispatchQueue = dispatchQueue;
    }

    public TriggerMode TriggerMode { get; set; } = TriggerMode.Polling;

    /// <summary>
    /// 启动工位调度生命周期。
    /// </summary>
    public async Task RunLifecycleAsync(TestStationModel station, CancellationToken cancellationToken)
    {
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

        switch (TriggerMode)
        {
            case TriggerMode.Polling:
                await RunPollingAsync(station, cancellationToken).ConfigureAwait(false);
                break;
            case TriggerMode.Programmatic:
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                break;
            case TriggerMode.InterruptDriven:
                await RunPollingAsync(station, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(TriggerMode), TriggerMode, null);
        }
    }
    public void RunLifecycleBlocking(TestStationModel station, CancellationToken cancellationToken)
    {
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

        switch (TriggerMode)
        {
            case TriggerMode.Polling:
            case TriggerMode.InterruptDriven:
                RunPollingBlocking(station, cancellationToken);
                break;
            case TriggerMode.Programmatic:
                cancellationToken.WaitHandle.WaitOne();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(TriggerMode), TriggerMode, null);
        }
    }

    public async Task ExecuteMeasurementAsync(bool triggerResult, TestStationModel station, CancellationToken cancellationToken)
    {
        Dictionary<string, double> previousValues = new(station.TestValues, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, bool> previousJudges = new(station.TestJudges, StringComparer.OrdinalIgnoreCase);

        if (station.ParallelDeals)
        {
            await Task.WhenAll(subDeals.Select(deal => deal.CollectAsync(triggerResult, station, cancellationToken))).ConfigureAwait(false);
        }
        else
        {
            foreach (IStationDataDeal deal in subDeals)
            {
                await deal.CollectAsync(triggerResult, station, cancellationToken).ConfigureAwait(false);
            }
        }

        StationResultMessage message = CreateResultMessage(station);

        // PLC/IO 握手必须先完成，后面的 UI、统计、保存都不能拖慢这条实时链路。
        if (TriggerMode != TriggerMode.Programmatic)
        {
            await machine.CompleteStationHandshakeAsync(station, message.IsPass, cancellationToken).ConfigureAwait(false);
        }

        if (dispatchQueue != null)
        {
            RestoreStationSnapshot(station, previousValues, previousJudges);
            dispatchQueue.TryEnqueue(message);
            return;
        }

        await machine.ProcessStationResultAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static StationResultMessage CreateResultMessage(TestStationModel station)
    {
        var values = new List<StationResultValue>();
        var testNames = new List<string>(station.OrderedTestNames);
        if (station.ShowInResultGrid)
        {
            foreach (string testName in station.TestValues.Keys)
            {
                if (!testNames.Contains(testName, StringComparer.OrdinalIgnoreCase))
                {
                    testNames.Add(testName);
                }
            }
        }

        foreach (string testName in testNames)
        {
            if (station.TestValues.TryGetValue(testName, out double value))
            {
                values.Add(new StationResultValue(
                    testName,
                    value,
                    station.TestJudges.TryGetValue(testName, out bool ok) ? ok : null));
            }
        }

        bool isPass = station.TestJudges.Count == 0 || station.TestJudges.All(static pair => pair.Value);
        return new StationResultMessage(station, values, isPass);
    }

    private static void RestoreStationSnapshot(
        TestStationModel station,
        IReadOnlyDictionary<string, double> values,
        IReadOnlyDictionary<string, bool> judges)
    {
        station.TestValues.Clear();
        foreach (KeyValuePair<string, double> pair in values)
        {
            station.TestValues[pair.Key] = pair.Value;
        }

        station.TestJudges.Clear();
        foreach (KeyValuePair<string, bool> pair in judges)
        {
            station.TestJudges[pair.Key] = pair.Value;
        }
    }

    private void RunPollingBlocking(TestStationModel station, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (machine.ReadStationTrigger(station))
            {
                bool triggerResult = station.StationIo.ResultSource == StationResultSource.Hardware
                    ? machine.ReadStationResult(station)
                    : true;
                ExecuteMeasurementAsync(triggerResult, station, cancellationToken).GetAwaiter().GetResult();
            }

            DelayPollingInterval(cancellationToken);
        }
    }
    private async Task RunPollingAsync(TestStationModel station, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (machine.ReadStationTrigger(station))
            {
                bool triggerResult = station.StationIo.ResultSource == StationResultSource.Hardware
                    ? machine.ReadStationResult(station)
                    : true;
                await ExecuteMeasurementAsync(triggerResult, station, cancellationToken).ConfigureAwait(false);
            }

            await DelayPollingIntervalAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DelayPollingIntervalAsync(CancellationToken cancellationToken)
    {
        int delayMs = machine.MachinePollingIntervalMs;
        if (delayMs <= 0)
        {
            Thread.Yield();
            return;
        }

        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
    }
    private void DelayPollingInterval(CancellationToken cancellationToken)
    {
        int delayMs = machine.MachinePollingIntervalMs;
        if (delayMs <= 0)
        {
            Thread.Yield();
            return;
        }

        if (cancellationToken.WaitHandle.WaitOne(delayMs))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}