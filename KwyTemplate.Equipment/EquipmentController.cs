using KwyTemplate.Process.Machines;

namespace KwyTemplate.Equipment;

/// <summary>
/// 统一进行整机命令准入、状态迁移，并将获准命令交给 Process 层执行。
/// </summary>
internal sealed class EquipmentController : IEquipmentController, IDisposable
{
    private readonly IMachine process;
    private readonly IReadOnlyList<IEquipmentCommandPolicy> policies;
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private EquipmentState state;
    private bool disposed;

    public EquipmentController(IMachine process, IEnumerable<IEquipmentCommandPolicy> policies)
    {
        this.process = process ?? throw new ArgumentNullException(nameof(process));
        this.policies = policies?.ToArray() ?? throw new ArgumentNullException(nameof(policies));
        state = process.IsRunning ? EquipmentState.Running : EquipmentState.Stopped;
        process.RunningStateChanged += OnProcessRunningStateChanged;
    }

    public event EventHandler? StateChanged;

    public EquipmentState State => state;

    public bool IsProductionRunning => state == EquipmentState.Running;

    public EquipmentCommandEvaluation Evaluate(EquipmentCommand command)
    {
        foreach (IEquipmentCommandPolicy policy in policies)
        {
            EquipmentCommandEvaluation evaluation = policy.Evaluate(command, state);
            if (!evaluation.IsAllowed)
            {
                return evaluation;
            }
        }

        return EquipmentCommandEvaluation.Allowed;
    }

    public Task<EquipmentCommandResult> StartProductionAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(EquipmentCommand.StartProduction, cancellationToken);

    public Task<EquipmentCommandResult> StopProductionAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(EquipmentCommand.StopProduction, cancellationToken);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        process.RunningStateChanged -= OnProcessRunningStateChanged;
        commandGate.Dispose();
    }

    private async Task<EquipmentCommandResult> ExecuteAsync(
        EquipmentCommand command,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EquipmentCommandEvaluation evaluation = Evaluate(command);
            if (!evaluation.IsAllowed)
            {
                return new EquipmentCommandResult(
                    false,
                    state,
                    evaluation.ReasonCode,
                    evaluation.Message);
            }

            SetState(command == EquipmentCommand.StartProduction
                ? EquipmentState.Starting
                : EquipmentState.Stopping);

            try
            {
                if (command == EquipmentCommand.StartProduction)
                {
                    await process.StartAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await process.StopAsync().ConfigureAwait(false);
                }

                SetState(process.IsRunning ? EquipmentState.Running : EquipmentState.Stopped);
                return new EquipmentCommandResult(true, state);
            }
            catch
            {
                // 命令失败不能把整机留在无法恢复的过渡态；
                // 以 Process 的实际状态回滚，让操作员可以重试。
                SetState(process.IsRunning ? EquipmentState.Running : EquipmentState.Stopped);
                throw;
            }
        }
        finally
        {
            commandGate.Release();
        }
    }

    private void OnProcessRunningStateChanged(object? sender, EventArgs e)
    {
        EquipmentState synchronizedState = process.IsRunning
            ? EquipmentState.Running
            : EquipmentState.Stopped;
        SetState(synchronizedState);
    }

    private void SetState(EquipmentState value)
    {
        if (state == value)
        {
            return;
        }

        state = value;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
