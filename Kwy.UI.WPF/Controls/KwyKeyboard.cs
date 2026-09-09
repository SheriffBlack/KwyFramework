using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Kwy.UI.WPF.Controls;

public enum KeyboardLayout
{
    Qwerty,
    Azerty,
    Qwertz
}

[TemplatePart(Name = "PART_KeysRoot", Type = typeof(Grid))]
[TemplatePart(Name = "PART_NumericKeysRoot", Type = typeof(Grid))]
public class KwyKeyboard : Control
{
    static KwyKeyboard()
    {
        // 设置默认样式键，确保控件能够找到对应的样式
        DefaultStyleKeyProperty.OverrideMetadata(typeof(KwyKeyboard),
            new FrameworkPropertyMetadata(typeof(KwyKeyboard)));
    }

    /// <summary>
    /// 存储键盘控件按钮主容器
    /// </summary>
    private Grid? keysRoot;

    public SoftKeyboardMode Mode
    {
        get => (SoftKeyboardMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(SoftKeyboardMode),
        typeof(KwyKeyboard),
        new PropertyMetadata(SoftKeyboardMode.Full));

    public bool AllowNegative
    {
        get => (bool)GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
    }

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.Register(
        nameof(AllowNegative), typeof(bool), typeof(KwyKeyboard), new PropertyMetadata(true));

    public Style? NumericKeyButtonStyle
    {
        get => (Style?)GetValue(NumericKeyButtonStyleProperty);
        set => SetValue(NumericKeyButtonStyleProperty, value);
    }

    public static readonly DependencyProperty NumericKeyButtonStyleProperty = DependencyProperty.Register(
        nameof(NumericKeyButtonStyle), typeof(Style), typeof(KwyKeyboard));

    public Style? NumericOperatorButtonStyle
    {
        get => (Style?)GetValue(NumericOperatorButtonStyleProperty);
        set => SetValue(NumericOperatorButtonStyleProperty, value);
    }

    public static readonly DependencyProperty NumericOperatorButtonStyleProperty = DependencyProperty.Register(
        nameof(NumericOperatorButtonStyle), typeof(Style), typeof(KwyKeyboard));

    public Style? NumericActionButtonStyle
    {
        get => (Style?)GetValue(NumericActionButtonStyleProperty);
        set => SetValue(NumericActionButtonStyleProperty, value);
    }

    public static readonly DependencyProperty NumericActionButtonStyleProperty = DependencyProperty.Register(
        nameof(NumericActionButtonStyle), typeof(Style), typeof(KwyKeyboard));

    public Style? NumericEnterButtonStyle
    {
        get => (Style?)GetValue(NumericEnterButtonStyleProperty);
        set => SetValue(NumericEnterButtonStyleProperty, value);
    }

    public static readonly DependencyProperty NumericEnterButtonStyleProperty = DependencyProperty.Register(
        nameof(NumericEnterButtonStyle), typeof(Style), typeof(KwyKeyboard));

    public string DecimalSeparator => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    public static readonly RoutedEvent KeyInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(KeyInvoked),
        RoutingStrategy.Bubble,
        typeof(EventHandler<KeyboardKeyInvokedEventArgs>),
        typeof(KwyKeyboard));

    public event EventHandler<KeyboardKeyInvokedEventArgs> KeyInvoked
    {
        add => AddHandler(KeyInvokedEvent, value);
        remove => RemoveHandler(KeyInvokedEvent, value);
    }

    /// <summary>
    /// 获取或设置默认键盘按钮的样式
    /// </summary>
    public Style? KwyKeyboardButtonStyle
    {
        get { return (Style?)GetValue(KwyKeyboardButtonStyleProperty); }
        set { SetValue(KwyKeyboardButtonStyleProperty, value); }
    }

    /// <summary>
    /// 标识 KwyKeyboardButtonStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty KwyKeyboardButtonStyleProperty =
        DependencyProperty.Register("KwyKeyboardButtonStyle", typeof(Style), typeof(KwyKeyboard),
            new PropertyMetadata(null, OnKwyKeyboardButtonStyleChanged));

    /// <summary>
    /// 获取或设置扩展键盘按钮的样式（如Shift、Alt、Ctrl键）
    /// </summary>
    public Style? ExtendButtonStyle
    {
        get { return (Style?)GetValue(ExtendButtonStyleProperty); }
        set { SetValue(ExtendButtonStyleProperty, value); }
    }

    /// <summary>
    /// 标识 ExtendButtonStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty ExtendButtonStyleProperty =
        DependencyProperty.Register("ExtendButtonStyle", typeof(Style), typeof(KwyKeyboard),
            new PropertyMetadata(null, OnExtendButtonStyleChanged));

    /// <summary>
    /// KwyKeyboardButtonStyle 属性变化时的回调函数
    /// </summary>
    private static void OnKwyKeyboardButtonStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加样式变化时的处理逻辑
    }

    /// <summary>
    /// ExtendButtonStyle 属性变化时的回调函数
    /// </summary>
    private static void OnExtendButtonStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加样式变化时的处理逻辑
    }

    /// <summary>
    /// 获取或设置是否开启Shift扩展
    /// <para>支持Shift+其他按钮进行组合</para>
    /// </summary>
    public bool IsShiftExtend
    {
        get { return (bool)GetValue(IsShiftExtendProperty); }
        set { SetValue(IsShiftExtendProperty, value); }
    }

    /// <summary>
    /// 标识 IsShiftExtend 依赖属性
    /// </summary>
    public static readonly DependencyProperty IsShiftExtendProperty =
        DependencyProperty.Register("IsShiftExtend", typeof(bool), typeof(KwyKeyboard),
            new PropertyMetadata(false, OnIsShiftExtendChanged));

    /// <summary>
    /// 获取或设置是否开启Alt扩展
    /// <para>支持Alt+其他按钮进行组合</para>
    /// </summary>
    public bool IsAltExtend
    {
        get { return (bool)GetValue(IsAltExtendProperty); }
        set { SetValue(IsAltExtendProperty, value); }
    }

    /// <summary>
    /// 标识 IsAltExtend 依赖属性
    /// </summary>
    public static readonly DependencyProperty IsAltExtendProperty =
        DependencyProperty.Register("IsAltExtend", typeof(bool), typeof(KwyKeyboard),
            new PropertyMetadata(false, OnIsAltExtendChanged));

    /// <summary>
    /// 获取或设置是否开启Ctrl扩展
    /// <para>支持Ctrl+其他按钮进行组合</para>
    /// </summary>
    public bool IsCtrlExtend
    {
        get { return (bool)GetValue(IsCtrlExtendProperty); }
        set { SetValue(IsCtrlExtendProperty, value); }
    }

    /// <summary>
    /// 标识 IsCtrlExtend 依赖属性
    /// </summary>
    public static readonly DependencyProperty IsCtrlExtendProperty =
        DependencyProperty.Register("IsCtrlExtend", typeof(bool), typeof(KwyKeyboard),
            new PropertyMetadata(false, OnIsCtrlExtendChanged));

    /// <summary>
    /// IsShiftExtend 属性变化时的回调函数
    /// </summary>
    private static void OnIsShiftExtendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加Shift扩展状态变化时的处理逻辑
    }

    /// <summary>
    /// IsAltExtend 属性变化时的回调函数
    /// </summary>
    private static void OnIsAltExtendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加Alt扩展状态变化时的处理逻辑
    }

    /// <summary>
    /// IsCtrlExtend 属性变化时的回调函数
    /// </summary>
    private static void OnIsCtrlExtendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加Ctrl扩展状态变化时的处理逻辑
    }

    /// <summary>
    /// 获取或设置键盘控件的圆角半径
    /// </summary>
    public CornerRadius CornerRadius
    {
        get { return (CornerRadius)GetValue(CornerRadiusProperty); }
        set { SetValue(CornerRadiusProperty, value); }
    }

    /// <summary>
    /// 标识 CornerRadius 依赖属性
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(KwyKeyboard));

    /// <summary>
    /// 获取或设置是否开启大写锁定
    /// </summary>
    public bool IsCapsLock
    {
        get { return (bool)GetValue(IsCapsLockProperty); }
        set { SetValue(IsCapsLockProperty, value); }
    }

    /// <summary>
    /// 标识 IsCapsLock 依赖属性
    /// </summary>
    public static readonly DependencyProperty IsCapsLockProperty =
        DependencyProperty.Register("IsCapsLock", typeof(bool), typeof(KwyKeyboard),
            new PropertyMetadata(false, OnIsCapsLockChanged));

    /// <summary>
    /// IsCapsLock 属性变化时的回调函数
    /// </summary>
    private static void OnIsCapsLockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加Caps Lock状态变化时的处理逻辑
    }

    /// <summary>
    /// 获取或设置键盘布局类型
    /// <para>支持的值：QWERTY、AZERTY、QWERTZ</para>
    /// </summary>
    public KeyboardLayout KeyboardLayout
    {
        get => (KeyboardLayout)GetValue(KeyboardLayoutProperty);
        set => SetValue(KeyboardLayoutProperty, value);
    }

    /// <summary>
    /// 标识 KeyboardLayout 依赖属性
    /// </summary>
    public static readonly DependencyProperty KeyboardLayoutProperty =
        DependencyProperty.Register(
            nameof(KeyboardLayout),
            typeof(KeyboardLayout),
            typeof(KwyKeyboard),
            new PropertyMetadata(KeyboardLayout.Qwerty, OnKeyboardLayoutChanged));

    /// <summary>
    /// KeyboardLayout 属性变化时的回调函数
    /// </summary>
    private static void OnKeyboardLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 可以在这里添加键盘布局变化时的处理逻辑
        // 例如：重新加载键盘布局、更新按键位置等
        var keyboard = d as KwyKeyboard;
        if (keyboard != null && keyboard.keysRoot != null)
        {
            // 移除旧的按键事件
            keyboard.AddOrRemoveKeyButtonEvent(false);
            // 可以在这里添加重新加载键盘布局的逻辑
            // 重新添加按键事件
            keyboard.AddOrRemoveKeyButtonEvent(true);
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        keysRoot = GetTemplateChild("PART_KeysRoot") as Grid;
        Loaded -= LayKeyboard_Loaded;
        Loaded += LayKeyboard_Loaded;
        Unloaded -= LayKeyboard_Unloaded;
        Unloaded += LayKeyboard_Unloaded;
    }

    /// <summary>
    /// 初始化给添加按钮点击事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LayKeyboard_Unloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= LayKeyboard_Unloaded;
        if (keysRoot != null)
        {
            AddOrRemoveKeyButtonEvent(false);
        }
    }

    /// <summary>
    /// 关闭时删除按钮点击事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LayKeyboard_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LayKeyboard_Loaded;
        if (keysRoot != null)
        {
            AddOrRemoveKeyButtonEvent(true);
        }
    }

    /// <summary>
    /// 给模拟键盘按钮新增或删除事件
    /// </summary>
    /// <param name="isAdd"></param>
    private void AddOrRemoveKeyButtonEvent(bool isAdd)
    {
        if (keysRoot == null) return;
        foreach (var button in EnumerateButtons(keysRoot))
        {
            if (isAdd)
            {
                button.Click -= Button_Click;
                button.Click += Button_Click;
            }
            else
            {
                button.Click -= Button_Click;
            }
        }
    }

    private static IEnumerable<ButtonBase> EnumerateButtons(DependencyObject root)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is ButtonBase button)
            {
                yield return button;
            }

            foreach (ButtonBase descendant in EnumerateButtons(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 开始进行文本内容填充
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ButtonBase button)
        {
            if (button.CommandParameter != null && button.CommandParameter is Key key)
            {
                if (key == Key.RightShift)
                {
                    IsShiftExtend = !IsShiftExtend;
                    IsAltExtend = false;
                    IsCtrlExtend = false;
                }
                else if (key == Key.RightAlt)
                {
                    IsShiftExtend = false;
                    IsAltExtend = !IsAltExtend;
                    IsCtrlExtend = false;
                }
                else if (key == Key.RightCtrl)
                {
                    IsShiftExtend = false;
                    IsAltExtend = false;
                    IsCtrlExtend = !IsCtrlExtend;
                }
                else
                {
                    // 处理Caps Lock键
                    if (key == Key.CapsLock)
                    {
                        IsCapsLock = !IsCapsLock;
                        return;
                    }

                    RaiseEvent(new KeyboardKeyInvokedEventArgs(
                        KeyInvokedEvent,
                        key,
                        IsShiftExtend,
                        IsCtrlExtend,
                        IsAltExtend,
                        IsCapsLock));

                    IsShiftExtend = false;
                    IsAltExtend = false;
                    IsCtrlExtend = false;
                }
            }
        }
    }
}
