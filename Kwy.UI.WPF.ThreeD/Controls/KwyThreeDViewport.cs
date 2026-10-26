using System.Windows;
using System.Windows.Controls;
using Kwy.UI.ThreeD.Abstractions.Rendering;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.ThreeD.Abstractions.Updates;

namespace Kwy.UI.WPF.ThreeD.Controls;

/// <summary>
/// Hosts a 3D viewport and owns its renderer-specific resources.
/// </summary>
public class KwyThreeDViewport : ContentControl, IDisposable
{
    public static readonly DependencyProperty BackendProperty = DependencyProperty.Register(
        nameof(Backend),
        typeof(IThreeDBackend<FrameworkElement>),
        typeof(KwyThreeDViewport),
        new PropertyMetadata(null, OnBackendChanged));

    public static readonly DependencyProperty SceneSourceProperty = DependencyProperty.Register(
        nameof(SceneSource),
        typeof(IThreeDSceneSource),
        typeof(KwyThreeDViewport),
        new PropertyMetadata(null, OnSceneSourceChanged));

    public static readonly DependencyProperty CameraProperty = DependencyProperty.Register(
        nameof(Camera),
        typeof(CameraState),
        typeof(KwyThreeDViewport),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnCameraChanged));

    private IThreeDViewport<FrameworkElement>? viewport;
    private bool disposed;

    public KwyThreeDViewport()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IThreeDBackend<FrameworkElement>? Backend
    {
        get => (IThreeDBackend<FrameworkElement>?)GetValue(BackendProperty);
        set => SetValue(BackendProperty, value);
    }

    public IThreeDSceneSource? SceneSource
    {
        get => (IThreeDSceneSource?)GetValue(SceneSourceProperty);
        set => SetValue(SceneSourceProperty, value);
    }

    public CameraState? Camera
    {
        get => (CameraState?)GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public IThreeDScene? Scene => viewport?.Scene;

    public event EventHandler<ThreeDHitEventArgs>? NodeHit;

    public void Apply(ThreeDSceneUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (viewport?.Scene is not { } scene)
            return;

        if (update is RemoveSceneNode remove)
        {
            scene.Remove(remove.NodeId);
            return;
        }

        if (!scene.TryGetNode(update.NodeId, out var node) || node is null)
            return;

        switch (update)
        {
            case SetNodeTransform transform:
                node.Transform = transform.Transform;
                break;
            case SetNodeVisibility visibility:
                node.IsVisible = visibility.IsVisible;
                break;
            case SetNodeMaterial material when node is IMaterialThreeDNode materialNode:
                materialNode.Material = material.Material;
                break;
        }
    }

    public void ZoomExtents(TimeSpan? animationDuration = null) => viewport?.ZoomExtents(animationDuration);

    public Task<RenderedImage> CaptureAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default) =>
        viewport?.CaptureAsync(width, height, cancellationToken)
        ?? throw new InvalidOperationException("The 3D viewport has not been created.");

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        ReleaseViewport();
    }

    private static void OnBackendChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDViewport)sender;
        if (!view.disposed)
            view.RecreateViewport();
    }

    private static void OnSceneSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDViewport)sender;
        view.PopulateScene();
    }

    private static void OnCameraChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDViewport)sender;
        if (view.viewport is not null && args.NewValue is CameraState camera)
            view.viewport.Camera = camera;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => EnsureViewport();

    private void OnUnloaded(object sender, RoutedEventArgs args) => ReleaseViewport();

    private void RecreateViewport()
    {
        ReleaseViewport();
        if (IsLoaded)
            EnsureViewport();
    }

    private void EnsureViewport()
    {
        if (viewport is not null || Backend is null)
            return;

        viewport = Backend.CreateViewport();
        viewport.Hit += OnViewportHit;
        if (Camera is not null)
            viewport.Camera = Camera;
        else
            SetCurrentValue(CameraProperty, viewport.Camera);

        Content = viewport.View;
        PopulateScene();
    }

    private void PopulateScene()
    {
        if (viewport is null)
            return;

        viewport.Scene.Clear();
        SceneSource?.Populate(viewport.Scene);
    }

    private void ReleaseViewport()
    {
        if (viewport is null)
            return;

        viewport.Hit -= OnViewportHit;
        Content = null;
        viewport.Dispose();
        viewport = null;
    }

    private void OnViewportHit(object? sender, ThreeDHitEventArgs args) => NodeHit?.Invoke(this, args);
}
