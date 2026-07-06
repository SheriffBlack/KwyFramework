using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

[TemplatePart(Name = PartTitleBar, Type = typeof(UIElement))]
[TemplatePart(Name = PartMinimizeButton, Type = typeof(Button))]
[TemplatePart(Name = PartMaximizeButton, Type = typeof(Button))]
[TemplatePart(Name = PartRestoreButton, Type = typeof(Button))]
[TemplatePart(Name = PartCloseButton, Type = typeof(Button))]
public class KwyWindow : Window
{
    private const string PartTitleBar = "PART_TitleBar";
    private const string PartMinimizeButton = "PART_MinimizeButton";
    private const string PartMaximizeButton = "PART_MaximizeButton";
    private const string PartRestoreButton = "PART_RestoreButton";
    private const string PartCloseButton = "PART_CloseButton";

    private UIElement? titleBar;
    private Button? minimizeButton;
    private Button? maximizeButton;
    private Button? restoreButton;
    private Button? closeButton;

    // 保存窗口恢复时的位置和大小
    private Rect restoreBounds = Rect.Empty;

    static KwyWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyWindow),
            new FrameworkPropertyMetadata(typeof(KwyWindow)));
    }

    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    public static readonly DependencyProperty TitleBarContentProperty =
        DependencyProperty.Register(
            nameof(TitleBarContent),
            typeof(object),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>
    /// 获取或设置最大化时是否覆盖任务栏。
    /// 如果为 true，窗口最大化时会覆盖任务栏（占据整个屏幕）。
    /// 如果为 false，窗口最大化时会排除任务栏（使用工作区域）。
    /// 默认值为 false。
    /// </summary>
    public bool MaximizeOverTaskbar
    {
        get => (bool)GetValue(MaximizeOverTaskbarProperty);
        set => SetValue(MaximizeOverTaskbarProperty, value);
    }

    public static readonly DependencyProperty MaximizeOverTaskbarProperty =
        DependencyProperty.Register(
            nameof(MaximizeOverTaskbar),
            typeof(bool),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(false));

    /// <summary>
    /// 获取或设置窗口是否已最大化到工作区域。
    /// 这是一个只读的依赖属性，用于样式绑定。
    /// </summary>
    public bool IsMaximizedToWorkArea
    {
        get => (bool)GetValue(IsMaximizedToWorkAreaProperty);
        private set => SetValue(IsMaximizedToWorkAreaPropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsMaximizedToWorkAreaPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsMaximizedToWorkArea),
            typeof(bool),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsMaximizedToWorkAreaProperty =
        IsMaximizedToWorkAreaPropertyKey.DependencyProperty;

    /// <summary>
    /// 获取或设置是否显示最小化按钮。
    /// 默认值为 true。
    /// </summary>
    public bool ShowMinimizeButton
    {
        get => (bool)GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    public static readonly DependencyProperty ShowMinimizeButtonProperty =
        DependencyProperty.Register(
            nameof(ShowMinimizeButton),
            typeof(bool),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(true));

    /// <summary>
    /// 获取或设置是否显示最大化按钮。
    /// 默认值为 true。
    /// </summary>
    public bool ShowMaximizeButton
    {
        get => (bool)GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    public static readonly DependencyProperty ShowMaximizeButtonProperty =
        DependencyProperty.Register(
            nameof(ShowMaximizeButton),
            typeof(bool),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(true));

    /// <summary>
    /// 获取或设置是否显示关闭按钮。
    /// 默认值为 true。
    /// </summary>
    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    public static readonly DependencyProperty ShowCloseButtonProperty =
        DependencyProperty.Register(
            nameof(ShowCloseButton),
            typeof(bool),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(true));

    /// <summary>
    /// 获取或设置是否显示用户切换按钮。
    /// 默认值为 false，避免窗口样式影响不需要用户入口的应用。
    /// </summary>
    public bool ShowUserButton
    {
        get => (bool)GetValue(ShowUserButtonProperty);
        set => SetValue(ShowUserButtonProperty, value);
    }

    public static readonly DependencyProperty ShowUserButtonProperty =
        DependencyProperty.Register(
            nameof(ShowUserButton),
            typeof(bool),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(false));

    /// <summary>
    /// 获取或设置用户切换按钮命令。
    /// </summary>
    public ICommand? UserCommand
    {
        get => (ICommand?)GetValue(UserCommandProperty);
        set => SetValue(UserCommandProperty, value);
    }

    public static readonly DependencyProperty UserCommandProperty =
        DependencyProperty.Register(
            nameof(UserCommand),
            typeof(ICommand),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// 获取或设置用户切换按钮命令参数。
    /// </summary>
    public object? UserCommandParameter
    {
        get => GetValue(UserCommandParameterProperty);
        set => SetValue(UserCommandParameterProperty, value);
    }

    public static readonly DependencyProperty UserCommandParameterProperty =
        DependencyProperty.Register(
            nameof(UserCommandParameter),
            typeof(object),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// 获取或设置标题栏高度。
    /// 默认值为 40。
    /// </summary>
    public double TitleBarHeight
    {
        get => (double)GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    public static readonly DependencyProperty TitleBarHeightProperty =
        DependencyProperty.Register(
            nameof(TitleBarHeight),
            typeof(double),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(40.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Brush? TitleBarBackground
    {
        get => (Brush?)GetValue(TitleBarBackgroundProperty);
        set => SetValue(TitleBarBackgroundProperty, value);
    }

    public static readonly DependencyProperty TitleBarBackgroundProperty =
        DependencyProperty.Register(
            nameof(TitleBarBackground),
            typeof(Brush),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// 获取或设置窗口图标。
    /// 支持字符串类型（字体图标）、Geometry 类型（路径图标）和 ImageSource 类型（图片）。
    /// </summary>
    public new object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public new static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(KwyWindow),
            new FrameworkPropertyMetadata(null));

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        AttachDragMove();
        AttachWindowButtons();
    }

    private void AttachDragMove()
    {
        if (this.titleBar != null)
        {
            this.titleBar.PreviewMouseLeftButtonDown -= TitleBarOnPreviewMouseLeftButtonDown;
            this.titleBar = null;
        }

        if (GetTemplateChild(PartTitleBar) is UIElement titleBar)
        {
            this.titleBar = titleBar;
            this.titleBar.PreviewMouseLeftButtonDown += TitleBarOnPreviewMouseLeftButtonDown;
        }
    }

    private void TitleBarOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 检查是否点击在交互元素上（优化：快速返回）
        if (IsClickOnInteractiveElement(e.OriginalSource))
        {
            return;
        }

        // 双击切换窗口状态
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        // DragMove() 必须在 MouseDown 事件中调用才能正常工作
        // 优化：使用 try-catch 避免异常，但不捕获鼠标（让 DragMove 内部处理）
        try
        {
            DragMove();
        }
        catch
        {
            // 忽略拖动异常（例如窗口未激活等情况）
        }
    }

    private void ToggleWindowState()
    {
        if (IsMaximizedToWorkArea)
        {
            RestoreFromWorkArea();
        }
        else
        {
            MaximizeToWorkArea();
        }
    }

    private static bool IsClickOnInteractiveElement(object source)
    {
        // 快速检查：如果源本身就是交互元素，直接返回
        if (source is Button or ToggleButton or TextBox or ComboBox or ComboBoxItem)
        {
            return true;
        }

        // 遍历视觉树查找交互元素（限制深度以提高性能）
        if (source is DependencyObject dependencyObject)
        {
            var current = dependencyObject;
            var maxDepth = 5; // 减少遍历深度，提高性能

            while (current != null && maxDepth-- > 0)
            {
                // 使用 switch 表达式提高性能
                switch (current)
                {
                    case Button:
                    case ComboBox:
                    case ComboBoxItem:
                    case Slider:
                    case ScrollBar:
                    case ToggleButton: // 包括 CheckBox、RadioButton
                    case TextBox:
                    case Viewbox:
                        return true;
                }

                // 检查是否有输入绑定（通常表示可交互）
                if (current is FrameworkElement element && element.InputBindings.Count > 0)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        return false;
    }

    private void AttachWindowButtons()
    {
        AttachButton(ref minimizeButton, PartMinimizeButton, OnMinimizeClick, ShowMinimizeButton);
        AttachButton(ref maximizeButton, PartMaximizeButton, OnMaximizeClick, ShowMaximizeButton);
        AttachButton(ref restoreButton, PartRestoreButton, OnRestoreClick, ShowMaximizeButton);
        AttachButton(ref closeButton, PartCloseButton, OnCloseClick, ShowCloseButton);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => MaximizeToWorkArea();

    private void OnRestoreClick(object sender, RoutedEventArgs e) => RestoreFromWorkArea();

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void MaximizeToWorkArea()
    {
        // 如果已经最大化，直接返回
        if (IsMaximizedToWorkArea)
        {
            return;
        }

        // 保存当前窗口位置和大小
        if (restoreBounds == Rect.Empty)
        {
            restoreBounds = new Rect(Left, Top, Width, Height);
        }

        // 保持 WindowState = Normal，但手动设置窗口位置和大小
        // 这样可以避免 WPF 窗口管理系统覆盖我们的设置
        WindowState = WindowState.Normal;

        ApplyWindowBounds(GetMaximizedBounds());

        IsMaximizedToWorkArea = true;
    }

    private void RestoreFromWorkArea()
    {
        // 如果已经恢复，直接返回
        if (!IsMaximizedToWorkArea)
        {
            return;
        }

        // 恢复窗口位置和大小
        if (restoreBounds != Rect.Empty)
        {
            WindowState = WindowState.Normal;
            ApplyWindowBounds(restoreBounds);
        }

        IsMaximizedToWorkArea = false;
    }

    private Rect GetMaximizedBounds()
    {
        if (MaximizeOverTaskbar)
        {
            return new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
        }

        return SystemParameters.WorkArea;
    }

    private void ApplyWindowBounds(Rect bounds)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            return;
        }

        var source = PresentationSource.FromVisual(this);
        var transformToDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var topLeft = transformToDevice.Transform(new Point(bounds.Left, bounds.Top));
        var bottomRight = transformToDevice.Transform(new Point(bounds.Right, bounds.Bottom));

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X - topLeft.X),
            (int)Math.Round(bottomRight.Y - topLeft.Y),
            SwpNoZOrder | SwpNoActivate);
    }

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    private void AttachButton(ref Button? buttonField, string partName, RoutedEventHandler handler, bool attach)
    {
        if (buttonField != null)
        {
            buttonField.Click -= handler;
            buttonField = null;
        }

        if (GetTemplateChild(partName) is Button button)
        {
            buttonField = button;
            if (attach)
            {
                button.Click += handler;
            }
        }
    }
}
