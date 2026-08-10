using System.Runtime.InteropServices;
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

    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    private UIElement? titleBar;
    private Button? minimizeButton;
    private Button? maximizeButton;
    private Button? restoreButton;
    private Button? closeButton;
    private HwndSource? hwndSource;

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
    /// false 使用当前屏幕工作区，true 使用当前屏幕完整区域。
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
    /// 获取窗口是否处于系统最大化状态，用于模板切换最大化/还原按钮。
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        hwndSource?.AddHook(WndProc);
        SyncMaximizedState();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        SyncMaximizedState();
    }

    protected override void OnClosed(EventArgs e)
    {
        hwndSource?.RemoveHook(WndProc);
        hwndSource = null;
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        AttachDragMove();
        AttachWindowButtons();
        SyncMaximizedState();
    }

    private void SyncMaximizedState()
        => IsMaximizedToWorkArea = WindowState == WindowState.Maximized;

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
        if (IsClickOnInteractiveElement(e.OriginalSource))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // 忽略拖动异常，例如鼠标状态被系统抢占。
        }
    }

    private void ToggleWindowState()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private static bool IsClickOnInteractiveElement(object source)
    {
        if (source is Button or ToggleButton or TextBox or ComboBox or ComboBoxItem)
        {
            return true;
        }

        if (source is DependencyObject dependencyObject)
        {
            var current = dependencyObject;
            var maxDepth = 5;

            while (current != null && maxDepth-- > 0)
            {
                switch (current)
                {
                    case Button:
                    case ComboBox:
                    case ComboBoxItem:
                    case Slider:
                    case ScrollBar:
                    case ToggleButton:
                    case TextBox:
                    case Viewbox:
                        return true;
                }

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

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => SystemCommands.MaximizeWindow(this);

    private void OnRestoreClick(object sender, RoutedEventArgs e) => SystemCommands.RestoreWindow(this);

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void ApplyMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        RECT target = MaximizeOverTaskbar ? monitorInfo.rcMonitor : monitorInfo.rcWork;
        RECT monitorRect = monitorInfo.rcMonitor;
        var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        minMaxInfo.ptMaxPosition.x = target.Left - monitorRect.Left;
        minMaxInfo.ptMaxPosition.y = target.Top - monitorRect.Top;
        minMaxInfo.ptMaxSize.x = target.Right - target.Left;
        minMaxInfo.ptMaxSize.y = target.Bottom - target.Top;

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

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

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}