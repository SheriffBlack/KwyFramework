using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Kwy.UI.ThreeD.Abstractions.Rendering;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.WPF.ThreeD.HelixToolkit.Scene;
using HelixCamera = HelixToolkit.Wpf.SharpDX.Camera;
using HelixOrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;
using HelixPerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using HelixProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;

namespace Kwy.UI.WPF.ThreeD.HelixToolkit.Rendering;

internal sealed class HelixThreeDViewport : IThreeDViewport<FrameworkElement>
{
    private readonly DefaultEffectsManager effectsManager = new();
    private readonly Viewport3DX viewport;
    private readonly HelixThreeDScene scene;
    private bool disposed;

    public HelixThreeDViewport(ThreeDViewportOptions options)
    {
        viewport = new Viewport3DX
        {
            EffectsManager = effectsManager,
            BackgroundColor = System.Windows.Media.Colors.Transparent,
            Camera = CreateCamera(new CameraState())
        };

        scene = new HelixThreeDScene(viewport);
        scene.Hit += OnSceneHit;
    }

    public FrameworkElement View => viewport;

    public IThreeDScene Scene => scene;

    public CameraState Camera
    {
        get => viewport.Camera is { } camera ? ReadCamera(camera) : new CameraState();
        set
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(value);
            viewport.Camera = CreateCamera(value);
        }
    }

    public event EventHandler<ThreeDHitEventArgs>? Hit;

    public void ZoomExtents(TimeSpan? animationDuration = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        viewport.ZoomExtents(animationDuration?.TotalMilliseconds ?? 0D);
    }

    public async Task<RenderedImage> CaptureAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        cancellationToken.ThrowIfCancellationRequested();

        return await viewport.Dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bitmap = viewport.RenderBitmap(width, height);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return new RenderedImage(stream.ToArray(), "image/png", width, height);
        });
    }

    public void Dispose()
    {
        if (disposed)
            return;

        if (!viewport.Dispatcher.CheckAccess())
            throw new InvalidOperationException("The 3D viewport must be disposed on its WPF dispatcher thread.");

        disposed = true;
        scene.Hit -= OnSceneHit;
        scene.Clear();
        viewport.EffectsManager = null;
        effectsManager.Dispose();
    }

    private void OnSceneHit(object? sender, ThreeDHitEventArgs args) => Hit?.Invoke(this, args);

    private static HelixCamera CreateCamera(CameraState source)
    {
        HelixProjectionCamera camera = source.Projection == CameraProjection.Orthographic
            ? new HelixOrthographicCamera { Width = source.OrthographicWidth }
            : new HelixPerspectiveCamera { FieldOfView = source.FieldOfViewRadians * 180D / Math.PI };

        camera.Position = new Point3D(source.Position.X, source.Position.Y, source.Position.Z);
        camera.LookDirection = new Vector3D(
            source.Target.X - source.Position.X,
            source.Target.Y - source.Position.Y,
            source.Target.Z - source.Position.Z);
        camera.UpDirection = new Vector3D(source.UpDirection.X, source.UpDirection.Y, source.UpDirection.Z);
        camera.NearPlaneDistance = source.NearPlaneDistance;
        camera.FarPlaneDistance = source.FarPlaneDistance;
        return camera;
    }

    private static CameraState ReadCamera(HelixCamera camera)
    {
        if (camera is not HelixProjectionCamera projection)
            return new CameraState();

        var position = new System.Numerics.Vector3(
            (float)projection.Position.X,
            (float)projection.Position.Y,
            (float)projection.Position.Z);
        var look = new System.Numerics.Vector3(
            (float)projection.LookDirection.X,
            (float)projection.LookDirection.Y,
            (float)projection.LookDirection.Z);

        return new CameraState
        {
            Position = position,
            Target = position + look,
            UpDirection = new System.Numerics.Vector3(
                (float)projection.UpDirection.X,
                (float)projection.UpDirection.Y,
                (float)projection.UpDirection.Z),
            Projection = projection is HelixOrthographicCamera
                ? CameraProjection.Orthographic
                : CameraProjection.Perspective,
            FieldOfViewRadians = projection is HelixPerspectiveCamera perspective
                ? (float)(perspective.FieldOfView * Math.PI / 180D)
                : MathF.PI / 4F,
            OrthographicWidth = projection is HelixOrthographicCamera orthographic
                ? (float)orthographic.Width
                : 10F,
            NearPlaneDistance = (float)projection.NearPlaneDistance,
            FarPlaneDistance = (float)projection.FarPlaneDistance
        };
    }
}
