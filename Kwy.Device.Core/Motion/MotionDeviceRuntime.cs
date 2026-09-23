using Kwy.Device.Abstractions.Motion;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 一张物理运动控制卡在 Core 中的运行时组合，统一持有卡、状态监视器、轴执行器和回零可信度生命周期。
/// 控制器进入新的连接会话时，会撤销该卡全部业务轴的原点可信度。
/// </summary>
public sealed class MotionDeviceRuntime : IMotionDeviceRuntime
{
    private readonly IDisposable? executorDisposable;
    private readonly IAxisHomeLifecycle? homeLifecycle;
    private int disposed;

    public MotionDeviceRuntime(
        IMotionCard card,
        IMotionStateMonitor stateMonitor,
        IAxisMotionExecutor axisExecutor,
        IAxisHomeLifecycle? homeLifecycle = null)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
        StateMonitor = stateMonitor ?? throw new ArgumentNullException(nameof(stateMonitor));
        AxisExecutor = axisExecutor ?? throw new ArgumentNullException(nameof(axisExecutor));
        executorDisposable = axisExecutor as IDisposable;
        this.homeLifecycle = homeLifecycle;
        Card.StateChanged += OnCardStateChanged;
    }

    public string DeviceId => Card.DeviceId;

    public IMotionCard Card { get; }

    public IMotionStateMonitor StateMonitor { get; }

    public IAxisMotionExecutor AxisExecutor { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Card.StateChanged -= OnCardStateChanged;
        executorDisposable?.Dispose();
        StateMonitor.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        Card.StateChanged -= OnCardStateChanged;
        executorDisposable?.Dispose();
        await StateMonitor.DisposeAsync().ConfigureAwait(false);
    }

    private void OnCardStateChanged(object? sender, ConnectionStateChangedEventArgs args)
    {
        if (args.CurrentState != ConnectionState.Connected || Card is not IAxisDefinitionProvider definitions || homeLifecycle is null)
            return;
        foreach (AxisDefinition axis in definitions.Axes)
            homeLifecycle.Invalidate(axis.Id, AxisHomeInvalidationReason.ControllerReconnected, "Motion controller entered a new connected session.");
    }
}
