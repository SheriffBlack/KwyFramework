using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Motion.Abstractions.Axes;

namespace Kwy.Device.Motion.Abstractions;

/// <summary>
/// 为运动状态监视器提供轴快照的采集源。
/// <para>
/// 实现可以使用轮询、专用线程、厂商回调、PDO 过程映像或混合方式；
/// 业务层和状态缓存不应依赖具体采集策略。
/// </para>
/// <para>
/// <see cref="StartAsync"/> 成功返回前，实现必须至少发布一帧完整初始状态，
/// 保证自动模式不会在无有效快照时启动。
/// </para>
/// <para>每个状态源归属一个监视器，不得在多个运行时之间共享。</para>
/// </summary>
public interface IMotionStateSource : IDisposable, IAsyncDisposable
{
    MotionStateAcquisitionMode AcquisitionMode { get; }

    bool IsRunning { get; }

    event Action<MotionAxisSnapshot>? SnapshotCaptured;

    event EventHandler<ErrorOccurredEventArgs>? SourceErrorOccurred;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
