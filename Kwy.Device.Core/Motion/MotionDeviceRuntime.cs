using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

public sealed class MotionDeviceRuntime : IMotionDeviceRuntime
{
    private readonly IDisposable? executorDisposable;
    private int disposed;

    public MotionDeviceRuntime(
        IMotionCard card,
        IMotionStateMonitor stateMonitor,
        IAxisMotionExecutor axisExecutor)
    {
        Card = card ?? throw new ArgumentNullException(nameof(card));
        StateMonitor = stateMonitor ?? throw new ArgumentNullException(nameof(stateMonitor));
        AxisExecutor = axisExecutor ?? throw new ArgumentNullException(nameof(axisExecutor));
        executorDisposable = axisExecutor as IDisposable;
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

        executorDisposable?.Dispose();
        StateMonitor.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        executorDisposable?.Dispose();
        await StateMonitor.DisposeAsync().ConfigureAwait(false);
    }
}
