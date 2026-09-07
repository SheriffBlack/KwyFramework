using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// Cyntec Reel 扫码 Feature。
/// 读取 MachineBase 的 DI 快照检测 Reel 扫上升沿，避免直接访问物理 IO 卡。
/// </summary>
public sealed class CyntecReelScanFeature : IMachineRuntimeFeature
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    private readonly ICyntecReelScanWorkflow reelScanWorkflow;
    private readonly CancellationTokenSource stopCts = new();
    private ICyntecReelScanMachine? reelScanMachine;
    private MachineBase? machine;
    private Task? worker;
    private bool? previousReelSignal;
    private bool disposed;

    public CyntecReelScanFeature(ICyntecReelScanWorkflow reelScanWorkflow)
    {
        this.reelScanWorkflow = reelScanWorkflow ?? throw new ArgumentNullException(nameof(reelScanWorkflow));
    }

    public bool CanAttach(MachineBase machine)
        => machine is ICyntecReelScanMachine;

    public void Start(MachineBase machine)
    {
        if (disposed || !CanAttach(machine) || worker is { IsCompleted: false })
        {
            return;
        }

        this.machine = machine;
        reelScanMachine = (ICyntecReelScanMachine)machine;
        previousReelSignal = null;
        worker = Task.Run(() => PollLoopAsync(stopCts.Token));
    }

    public void Stop()
    {
        stopCts.Cancel();
        try
        {
            worker?.Wait(StopTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is OperationCanceledException))
        {
        }
        catch
        {
        }
        finally
        {
            worker = null;
            previousReelSignal = null;
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (machine == null || reelScanMachine == null)
                {
                    await Task.Delay(RetryInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!machine.TryReadDiSnapshotBit(reelScanMachine.ReelScanInputChannel, out bool current))
                {
                    await Task.Delay(RetryInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!previousReelSignal.HasValue)
                {
                    previousReelSignal = current;
                }
                else if (current && !previousReelSignal.Value)
                {
                    await reelScanWorkflow.ScanAsync(cancellationToken).ConfigureAwait(false);
                    previousReelSignal = current;
                }
                else
                {
                    previousReelSignal = current;
                }

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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Stop();
        stopCts.Dispose();
    }
}