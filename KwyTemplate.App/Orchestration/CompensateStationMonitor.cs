using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// 补偿/点检页面专用的工站完成信号监听器。
/// 只读取 MachineBase 的 DI 快照，并在监听器内部保存 lastState 判断上升沿，避免消费机台实时线程的边沿锁存。
/// </summary>
public sealed class CompensateStationMonitor : IDisposable
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(1);
    private readonly MachineBase machine;
    private readonly Dictionary<int, bool> previousStationSignals = [];
    private CancellationTokenSource? cts;
    private Task? monitorTask;
    private long monitorVersion;

    public CompensateStationMonitor(MachineBase machine)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
    }

    public void Start(Func<CompensateStationMeasurement, CancellationToken, Task> measurementHandler)
    {
        ArgumentNullException.ThrowIfNull(measurementHandler);
        Stop();
        InitializeStationSignalSnapshot();

        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;
        long version = Interlocked.Increment(ref monitorVersion);

        monitorTask = Task.Factory.StartNew(
            () => RunAsync(measurementHandler, version, token),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    public void Stop()
    {
        CancellationTokenSource? current = cts;
        if (current == null)
        {
            return;
        }

        cts = null;
        current.Cancel();
        current.Dispose();
        Interlocked.Increment(ref monitorVersion);
        monitorTask = null;
        previousStationSignals.Clear();
    }

    private void InitializeStationSignalSnapshot()
    {
        previousStationSignals.Clear();
        foreach (TestStationModel station in machine.TestStations)
        {
            int channel = station.StationIo.TestFinishedInput;
            if (channel < 0)
            {
                continue;
            }

            previousStationSignals[station.StationId] = machine.TryReadDiSnapshotBit(channel, out bool state) && state;
        }
    }

    private void RefreshStationSignalSnapshot()
    {
        foreach (TestStationModel station in machine.TestStations)
        {
            int channel = station.StationIo.TestFinishedInput;
            if (channel < 0)
            {
                continue;
            }

            previousStationSignals[station.StationId] = machine.TryReadDiSnapshotBit(channel, out bool state) && state;
        }
    }
    private async Task RunAsync(
        Func<CompensateStationMeasurement, CancellationToken, Task> measurementHandler,
        long version,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && version == Volatile.Read(ref monitorVersion))
        {
            try
            {
                if (machine.ProductionState == MachineProductionState.Running)
                {
                    RefreshStationSignalSnapshot();
                    await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                foreach (TestStationModel station in machine.TestStations)
                {
                    if (!IsStationSignalRising(station))
                    {
                        continue;
                    }

                    IStationInstrumentOperation? operation = station.StationDataDeals
                        .OfType<IStationInstrumentOperation>()
                        .FirstOrDefault();
                    if (operation == null)
                    {
                        continue;
                    }

                    InstrumentMeasurementResult measurement = await operation.ReadDisplayMeasurementAsync(cancellationToken).ConfigureAwait(false);
                    await measurementHandler(new CompensateStationMeasurement(station, operation.TestName, measurement), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // 监听线程不能因为单次仪表或业务异常退出；下一轮触发可恢复。
            }

            await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsStationSignalRising(TestStationModel station)
    {
        int channel = station.StationIo.TestFinishedInput;
        if (channel < 0 || !machine.TryReadDiSnapshotBit(channel, out bool current))
        {
            return false;
        }

        bool previous = previousStationSignals.TryGetValue(station.StationId, out bool old) && old;
        previousStationSignals[station.StationId] = current;
        return current && !previous;
    }

    public void Dispose()
        => Stop();
}

public sealed record CompensateStationMeasurement(
    TestStationModel Station,
    string TestName,
    InstrumentMeasurementResult Measurement);
