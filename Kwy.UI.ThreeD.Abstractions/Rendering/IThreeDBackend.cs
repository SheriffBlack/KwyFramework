using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.ThreeD.Abstractions.Primitives;

namespace Kwy.UI.ThreeD.Abstractions.Rendering;

public interface IThreeDBackend
{
    string Name { get; }

    ThreeDCapabilities Capabilities { get; }
}

/// <summary>
/// 创建由表示层框架所选 UI 元素类型承载的视口。
/// WPF 实现通常使用 FrameworkElement 作为 <typeparamref name="TView"/>。
/// </summary>
public interface IThreeDBackend<TView> : IThreeDBackend
    where TView : class
{
    IThreeDSession<TView> Create(ThreeDViewOptions? options = null);
}

/// <summary>
/// 表示一个由具体渲染后端创建并维护的 3D 会话。
/// </summary>
public interface IThreeDSession : IDisposable
{
    IThreeDScene Scene { get; }

    CameraState Camera { get; set; }

    LinearColor BackgroundColor { get; set; }

    bool IsNavigationEnabled { get; set; }

    event EventHandler<ThreeDHitEventArgs>? Hit;

    event EventHandler? CameraChanged;

    void ZoomExtents(TimeSpan? animationDuration = null);

    Task<RenderedImage> CaptureAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default);
}

public interface IThreeDSession<out TView> : IThreeDSession
    where TView : class
{
    TView View { get; }
}

public sealed record ThreeDViewOptions
{
    public int? PreferredFramesPerSecond { get; init; }

    public LinearColor BackgroundColor { get; init; } = LinearColor.Transparent;

    public bool IsNavigationEnabled { get; init; } = true;
}

public sealed record RenderedImage(
    ReadOnlyMemory<byte> Data,
    string MediaType,
    int Width,
    int Height);
