using KwyTemplate.App.Runtime;
using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// PLC 停机信号轮询 Feature。
/// 统一轮询当前机型暴露的停机信号点位，例如编带电机释放、点检过期一卷完成、标准件过期一卷完成。
/// 读到有效信号后调用 Machine.StopAsync()，从而触发机台 OnTestStopped 流程。
/// </summary>
public sealed class MachinePlcStopSignalFeature : IMachineRuntimeFeature
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private readonly IProductionContext productionContext;
    private readonly HashSet<MachinePlcStopSignalKind> handledSignals = [];
    private readonly object syncRoot = new();
    private MachineBase? machine;
    private IMachinePlcStopSignalMachine? stopSignalMachine;
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private bool disposed;

    public MachinePlcStopSignalFeature(IProductionContext productionContext)
    {
        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
    }

    public bool CanAttach(MachineBase machine)
        => machine is IMachinePlcStopSignalMachine;

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

            this.machine = machine;
            stopSignalMachine = (IMachinePlcStopSignalMachine)machine;
            handledSignals.Clear();
            stopCts = new CancellationTokenSource();
            worker = PollLoopAsync(stopCts.Token);
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
            stopSignalMachine = null;
            handledSignals.Clear();
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
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
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

    /// <summary>
    /// 10ms 周期轮询 PLC 停机信号。设备未就绪或异常时降低频率重试，避免异常刷屏。
    /// </summary>
    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IMachinePlcStopSignalMachine? currentStopSignalMachine = stopSignalMachine;
                MachineBase? currentMachine = machine;
                if (currentStopSignalMachine == null || currentMachine == null)
                {
                    await Task.Delay(RetryInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                IReadOnlyList<MachinePlcStopSignal> activeSignals =
                    await currentStopSignalMachine.ReadPlcStopSignalsAsync(cancellationToken).ConfigureAwait(false);

                await HandleActiveSignalsAsync(currentMachine, currentStopSignalMachine, activeSignals, cancellationToken)
                    .ConfigureAwait(false);

                await Task.Delay(PollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                await DelaySafelyAsync(RetryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 处理当前有效停机信号。
    /// 同一个信号保持为 1 时只处理一次，等信号回到 0 后才允许下次重新触发。
    /// 编带电机释放会额外清空台纸并复位该 PLC 点位；其他停机信号只负责停机。
    /// </summary>
    private async Task HandleActiveSignalsAsync(
        MachineBase currentMachine,
        IMachinePlcStopSignalMachine currentStopSignalMachine,
        IReadOnlyList<MachinePlcStopSignal> activeSignals,
        CancellationToken cancellationToken)
    {
        HashSet<MachinePlcStopSignalKind> activeKinds = activeSignals.Select(signal => signal.Kind).ToHashSet();
        handledSignals.RemoveWhere(kind => !activeKinds.Contains(kind));

        foreach (MachinePlcStopSignal signal in activeSignals)
        {
            if (!handledSignals.Add(signal.Kind))
            {
                continue;
            }

            await currentMachine.StopAsync().ConfigureAwait(false);

            if (signal.ClearTablePaperCode)
            {
                productionContext.TablePaperCode = string.Empty;
            }

            if (signal.ResetAfterHandled)
            {
                await currentStopSignalMachine.ResetPlcStopSignalAsync(signal.Kind, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}