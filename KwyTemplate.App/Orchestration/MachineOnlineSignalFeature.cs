using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

/// <summary>
/// 工控机在线信号 Feature。
/// 启动时有限重试写 IO 工控机在线 = true；停止或退出时写回 false。
/// </summary>
public sealed class MachineOnlineSignalFeature : IMachineRuntimeFeature
{
    private const int MaxOnlineSetAttempts = 3;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);
    private readonly object syncRoot = new();
    private IIndustrialPcOnlineSignalMachine? onlineMachine;
    private CancellationTokenSource? stopCts;
    private Task? worker;
    private bool disposed;

    public int ShutdownOrder => -100;

    public bool CanAttach(MachineBase machine)
        => machine is IIndustrialPcOnlineSignalMachine;

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

            onlineMachine = (IIndustrialPcOnlineSignalMachine)machine;
            stopCts = new CancellationTokenSource();
            worker = SetOnlineWithRetryAsync(stopCts.Token);
        }
    }

    public void Stop()
    {
        IIndustrialPcOnlineSignalMachine? machine;
        CancellationTokenSource? cts;
        Task? runningWorker;

        lock (syncRoot)
        {
            machine = onlineMachine;
            cts = stopCts;
            runningWorker = worker;
            stopCts = null;
            worker = null;
        }

        if (cts != null)
        {
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

        if (machine == null)
        {
            return;
        }

        try
        {
            machine.SetIndustrialPcOnlineAsync(false, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch
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
    }

    /// <summary>
    /// 启动时 IO 卡可能还没完全就绪，所以允许短暂重试；超过次数后不做长期心跳。
    /// </summary>
    private async Task SetOnlineWithRetryAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxOnlineSetAttempts && !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                if (onlineMachine != null
                    && await onlineMachine.SetIndustrialPcOnlineAsync(true, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
            }

            if (attempt < MaxOnlineSetAttempts)
            {
                try
                {
                    await Task.Delay(RetryInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
