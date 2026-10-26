using Kwy.UI.ThreeD.Abstractions.Scene;

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
    IThreeDViewport<TView> CreateViewport(ThreeDViewportOptions? options = null);
}

public interface IThreeDViewport : IDisposable
{
    IThreeDScene Scene { get; }

    CameraState Camera { get; set; }

    event EventHandler<ThreeDHitEventArgs>? Hit;

    void ZoomExtents(TimeSpan? animationDuration = null);

    Task<RenderedImage> CaptureAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default);
}

public interface IThreeDViewport<out TView> : IThreeDViewport
    where TView : class
{
    TView View { get; }
}

public sealed record ThreeDViewportOptions
{
    public int? PreferredFramesPerSecond { get; init; }
}

public sealed record RenderedImage(
    ReadOnlyMemory<byte> Data,
    string MediaType,
    int Width,
    int Height);
