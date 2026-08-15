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
        if (station.StationIo.ResultSource == StationResultSource.Hardware && dispatchQueue != null)
        {
            IReadOnlyList<CapturedStationDataDeal> capturedMeasurements = await CaptureMeasurementAsync(station, cancellationToken).ConfigureAwait(false);
            StationResultMessage deferredMessage = StationResultMessage.CreateDeferredHardware(station, triggerResult, capturedMeasurements);
            dispatchQueue.TryEnqueue(deferredMessage);

            if (TriggerMode != TriggerMode.Programmatic)
            {
                await machine.CompleteStationHandshakeAsync(station, triggerResult, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

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

        StationResultMessage message = StationResultMessage.Create(station);

        // PLC/IO 握手必须先完成，后面的 UI、统计、保存都不能拖慢这条实时链路。
        if (dispatchQueue != null)
        {
            RestoreStationSnapshot(station, previousValues, previousJudges);
            if (TriggerMode != TriggerMode.Programmatic)
            {
                await machine.CompleteStationHandshakeAsync(station, message.IsPass, cancellationToken).ConfigureAwait(false);
            }

            dispatchQueue.TryEnqueue(message);
            return;
        }

        await machine.ProcessStationResultAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CapturedStationDataDeal>> CaptureMeasurementAsync(TestStationModel station, CancellationToken cancellationToken)
    {
        if (station.ParallelDeals)
        {
            CapturedStationDataDeal[] capturedMeasurements = await Task.WhenAll(subDeals
                .Select(async deal => new CapturedStationDataDeal(deal, await deal.CaptureAsync(cancellationToken).ConfigureAwait(false))))
                .ConfigureAwait(false);
            return capturedMeasurements;
        }

        var captures = new List<CapturedStationDataDeal>(subDeals.Count);
        foreach (IStationDataDeal deal in subDeals)
        {
            IStationDataCapture capture = await deal.CaptureAsync(cancellationToken).ConfigureAwait(false);
            captures.Add(new CapturedStationDataDeal(deal, capture));
        }
        return captures;
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
