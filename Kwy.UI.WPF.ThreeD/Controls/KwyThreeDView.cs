using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kwy.UI.ThreeD.Abstractions.Primitives;
using Kwy.UI.ThreeD.Abstractions.Rendering;
using Kwy.UI.ThreeD.Abstractions.Scene;
using Kwy.UI.ThreeD.Abstractions.Updates;

namespace Kwy.UI.WPF.ThreeD.Controls;

/// <summary>
/// 托管一个 3D 渲染会话，而不向应用程序视图暴露具体渲染引擎。
/// </summary>
public class KwyThreeDView : ContentControl, IThreeDUpdateSink, IDisposable
{
    private const int MaxReplayUpdates = 100_000;

    public static readonly DependencyProperty BackendProperty = DependencyProperty.Register(
        nameof(Backend),
        typeof(IThreeDBackend<FrameworkElement>),
        typeof(KwyThreeDView),
        new PropertyMetadata(null, OnBackendChanged));

    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(IThreeDSceneSource),
        typeof(KwyThreeDView),
        new PropertyMetadata(null, OnSceneChanged));

    public static readonly DependencyProperty CameraProperty = DependencyProperty.Register(
        nameof(Camera),
        typeof(CameraState),
        typeof(KwyThreeDView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnCameraChanged));

    public static readonly DependencyProperty IsNavigationEnabledProperty = DependencyProperty.Register(
        nameof(IsNavigationEnabled),
        typeof(bool),
        typeof(KwyThreeDView),
        new PropertyMetadata(true, OnNavigationChanged));

    public static readonly DependencyProperty PreferredFramesPerSecondProperty = DependencyProperty.Register(
        nameof(PreferredFramesPerSecond),
        typeof(int?),
        typeof(KwyThreeDView),
        new PropertyMetadata(null, OnBackendChanged));

    public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
        nameof(SelectedObject),
        typeof(IThreeDNode),
        typeof(KwyThreeDView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HitTestCommandProperty = DependencyProperty.Register(
        nameof(HitTestCommand),
        typeof(ICommand),
        typeof(KwyThreeDView));

    private IThreeDSession<FrameworkElement>? session;
    private readonly List<ThreeDSceneUpdate> replayUpdates = [];
    private bool synchronizingCamera;
    private bool disposed;

    static KwyThreeDView()
    {
        BackgroundProperty.OverrideMetadata(
            typeof(KwyThreeDView),
            new FrameworkPropertyMetadata(Brushes.Transparent, OnBackgroundChanged));
    }

    public KwyThreeDView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IThreeDBackend<FrameworkElement>? Backend
    {
        get => (IThreeDBackend<FrameworkElement>?)GetValue(BackendProperty);
        set => SetValue(BackendProperty, value);
    }

    public IThreeDSceneSource? Scene
    {
        get => (IThreeDSceneSource?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public CameraState? Camera
    {
        get => (CameraState?)GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public bool IsNavigationEnabled
    {
        get => (bool)GetValue(IsNavigationEnabledProperty);
        set => SetValue(IsNavigationEnabledProperty, value);
    }

    public int? PreferredFramesPerSecond
    {
        get => (int?)GetValue(PreferredFramesPerSecondProperty);
        set => SetValue(PreferredFramesPerSecondProperty, value);
    }

    public IThreeDNode? SelectedObject
    {
        get => (IThreeDNode?)GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public ICommand? HitTestCommand
    {
        get => (ICommand?)GetValue(HitTestCommandProperty);
        set => SetValue(HitTestCommandProperty, value);
    }

    /// <summary>
    /// 当前由后端创建的会话；未加载或尚未配置后端时为 null。
    /// </summary>
    public IThreeDSession? Session => session;

    /// <summary>
    /// 当前会话中的可变场景；通常由门面方法和更新消息操作。
    /// </summary>
    public IThreeDScene? RenderedScene => session?.Scene;

    public event EventHandler<ThreeDHitEventArgs>? NodeHit;

    public void Apply(ThreeDSceneUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Remember(update);
        if (session?.Scene is { } renderedScene)
            ApplyToScene(renderedScene, update);
    }

    /// <summary>
    /// 清除增量更新，并从绑定的场景源恢复基础场景。
    /// </summary>
    public void Reset()
    {
        replayUpdates.Clear();
        PopulateScene();
    }

    private static void ApplyToScene(IThreeDScene renderedScene, ThreeDSceneUpdate update)
    {

        switch (update)
        {
            case AddSceneNode add:
                renderedScene.Add(add.Node);
                return;
            case RemoveSceneNode remove:
                renderedScene.Remove(remove.NodeId);
                return;
            case SetSceneLights lights:
                renderedScene.SetLights(lights.Lights);
                return;
            case ClearThreeDScene:
                renderedScene.Clear();
                return;
        }

        var nodeId = update switch
        {
            SetNodeTransform transform => transform.NodeId,
            SetNodeVisibility visibility => visibility.NodeId,
            SetNodeHitTestVisibility hitTestVisibility => hitTestVisibility.NodeId,
            SetNodeMaterial material => material.NodeId,
            _ => Guid.Empty
        };

        if (nodeId == Guid.Empty || !renderedScene.TryGetNode(nodeId, out var node) || node is null)
            return;

        switch (update)
        {
            case SetNodeTransform transform:
                node.Transform = transform.Transform;
                break;
            case SetNodeVisibility visibility:
                node.IsVisible = visibility.IsVisible;
                break;
            case SetNodeHitTestVisibility hitTestVisibility:
                node.IsHitTestVisible = hitTestVisibility.IsHitTestVisible;
                break;
            case SetNodeMaterial material when node is IMaterialThreeDNode materialNode:
                materialNode.Material = material.Material;
                break;
        }
    }

    public void ZoomExtents(TimeSpan? animationDuration = null) => session?.ZoomExtents(animationDuration);

    public Task<RenderedImage> CaptureAsync(
        int width,
        int height,
        CancellationToken cancellationToken = default) =>
        session?.CaptureAsync(width, height, cancellationToken)
        ?? throw new InvalidOperationException("3D 会话尚未创建。");

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        ReleaseSession();
        replayUpdates.Clear();
    }

    private static void OnBackendChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDView)sender;
        if (!view.disposed)
            view.RecreateSession();
    }

    private static void OnSceneChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((KwyThreeDView)sender).PopulateScene();

    private static void OnCameraChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDView)sender;
        if (!view.synchronizingCamera && view.session is not null && args.NewValue is CameraState camera)
            view.session.Camera = camera;
    }

    private static void OnNavigationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDView)sender;
        if (view.session is not null)
            view.session.IsNavigationEnabled = (bool)args.NewValue;
    }

    private static void OnBackgroundChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (KwyThreeDView)sender;
        if (view.session is not null)
            view.session.BackgroundColor = ToLinearColor(args.NewValue as Brush);
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => EnsureSession();

    private void OnUnloaded(object sender, RoutedEventArgs args) => ReleaseSession();

    private void RecreateSession()
    {
        ReleaseSession();
        if (IsLoaded)
            EnsureSession();
    }

    private void EnsureSession()
    {
        if (session is not null || Backend is null)
            return;

        session = Backend.Create(new ThreeDViewOptions
        {
            PreferredFramesPerSecond = PreferredFramesPerSecond,
            BackgroundColor = ToLinearColor(Background),
            IsNavigationEnabled = IsNavigationEnabled
        });
        session.Hit += OnSessionHit;
        session.CameraChanged += OnSessionCameraChanged;

        if (Camera is not null)
            session.Camera = Camera;
        else
            UpdateCameraFromSession();

        Content = session.View;
        PopulateScene();
    }

    private void PopulateScene()
    {
        if (session is null)
            return;

        session.Scene.Clear();
        SetCurrentValue(SelectedObjectProperty, null);
        Scene?.Populate(session.Scene);
        foreach (var update in replayUpdates)
            ApplyToScene(session.Scene, update);
    }

    private void ReleaseSession()
    {
        if (session is null)
            return;

        session.Hit -= OnSessionHit;
        session.CameraChanged -= OnSessionCameraChanged;
        Content = null;
        session.Dispose();
        session = null;
        SetCurrentValue(SelectedObjectProperty, null);
    }

    private void OnSessionHit(object? sender, ThreeDHitEventArgs args)
    {
        SetCurrentValue(SelectedObjectProperty, args.Result.Node);
        NodeHit?.Invoke(this, args);

        if (HitTestCommand?.CanExecute(args.Result) == true)
            HitTestCommand.Execute(args.Result);
    }

    private void OnSessionCameraChanged(object? sender, EventArgs args) => UpdateCameraFromSession();

    private void UpdateCameraFromSession()
    {
        if (session is null)
            return;

        synchronizingCamera = true;
        try
        {
            SetCurrentValue(CameraProperty, session.Camera);
        }
        finally
        {
            synchronizingCamera = false;
        }
    }

    private static LinearColor ToLinearColor(Brush? brush)
    {
        if (brush is not SolidColorBrush solid)
            return LinearColor.Transparent;

        var color = solid.Color;
        return new LinearColor(
            color.ScR,
            color.ScG,
            color.ScB,
            Math.Clamp(color.ScA * (float)solid.Opacity, 0F, 1F));
    }

    private void Remember(ThreeDSceneUpdate update)
    {
        switch (update)
        {
            case ClearThreeDScene:
                replayUpdates.Clear();
                replayUpdates.Add(update);
                SetCurrentValue(SelectedObjectProperty, null);
                return;
            case SetSceneLights:
                replayUpdates.RemoveAll(static item => item is SetSceneLights);
                break;
            case SetNodeTransform transform:
                replayUpdates.RemoveAll(item => item is SetNodeTransform current && current.NodeId == transform.NodeId);
                break;
            case SetNodeVisibility visibility:
                replayUpdates.RemoveAll(item => item is SetNodeVisibility current && current.NodeId == visibility.NodeId);
                break;
            case SetNodeHitTestVisibility hitTestVisibility:
                replayUpdates.RemoveAll(item => item is SetNodeHitTestVisibility current && current.NodeId == hitTestVisibility.NodeId);
                break;
            case SetNodeMaterial material:
                replayUpdates.RemoveAll(item => item is SetNodeMaterial current && current.NodeId == material.NodeId);
                break;
            case RemoveSceneNode remove:
                replayUpdates.RemoveAll(item => item switch
                {
                    SetNodeTransform current => current.NodeId == remove.NodeId,
                    SetNodeVisibility current => current.NodeId == remove.NodeId,
                    SetNodeHitTestVisibility current => current.NodeId == remove.NodeId,
                    SetNodeMaterial current => current.NodeId == remove.NodeId,
                    _ => false
                });
                break;
        }

        replayUpdates.Add(update);
        while (replayUpdates.Count > MaxReplayUpdates)
            replayUpdates.RemoveAt(0);
    }
}
