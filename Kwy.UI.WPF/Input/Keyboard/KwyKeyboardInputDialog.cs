using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kwy.UI.WPF.Controls;

namespace Kwy.UI.WPF.Input.Keyboard;

internal sealed partial class KwyKeyboardInputDialog : KwyWindow
{
    private TextBoxKeyboardInputAdapter? inputAdapter;
    private bool isSessionActive;
    private bool allowPermanentClose;

    public KwyKeyboardInputDialog()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
        PART_Keyboard.KeyInvoked += OnKeyInvoked;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(KwyKeyboardInputDialog),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public KwyKeyboardMode Mode
    {
        get => (KwyKeyboardMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(KwyKeyboardMode),
        typeof(KwyKeyboardInputDialog),
        new PropertyMetadata(KwyKeyboardMode.Full),
        static value => value is KwyKeyboardMode mode && Enum.IsDefined(mode));

    public KeyboardLayout KeyboardLayout
    {
        get => (KeyboardLayout)GetValue(KeyboardLayoutProperty);
        set => SetValue(KeyboardLayoutProperty, value);
    }

    public static readonly DependencyProperty KeyboardLayoutProperty = DependencyProperty.Register(
        nameof(KeyboardLayout),
        typeof(KeyboardLayout),
        typeof(KwyKeyboardInputDialog),
        new PropertyMetadata(KeyboardLayout.Qwerty),
        static value => value is KeyboardLayout layout && Enum.IsDefined(layout));

    public bool AllowNegative
    {
        get => (bool)GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
    }

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.Register(
        nameof(AllowNegative),
        typeof(bool),
        typeof(KwyKeyboardInputDialog),
        new PropertyMetadata(true));

    public double KeyboardWidth
    {
        get => (double)GetValue(KeyboardWidthProperty);
        set => SetValue(KeyboardWidthProperty, value);
    }

    public static readonly DependencyProperty KeyboardWidthProperty = DependencyProperty.Register(
        nameof(KeyboardWidth),
        typeof(double),
        typeof(KwyKeyboardInputDialog),
        new PropertyMetadata(760d),
        static value => value is double width && double.IsFinite(width) && width > 0);

    internal bool IsConfirmed { get; private set; }

    internal void Prepare(NumericKeyboardOptions? numericOptions)
    {
        IsConfirmed = false;
        isSessionActive = true;
        inputAdapter = new TextBoxKeyboardInputAdapter(PART_Editor, numericOptions);
    }

    internal void Prewarm()
    {
        ApplyTemplate();
        PART_Editor.ApplyTemplate();
        PART_Keyboard.ApplyTemplate();
    }

    internal void CancelSession() => Complete(false);

    internal void DisposePermanently()
    {
        allowPermanentClose = true;
        isSessionActive = false;
        Close();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        PART_Editor.Focus();
        PART_Editor.SelectAll();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Complete(false);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Complete(true);
        }
    }

    private void OnKeyInvoked(object? sender, KeyboardKeyInvokedEventArgs e)
    {
        if (inputAdapter == null)
        {
            return;
        }

        KeyboardInputResult result = inputAdapter.HandleKey(e, KeyboardLayout);
        e.Handled = true;
        if (result == KeyboardInputResult.Commit)
        {
            Complete(true);
        }
        else if (result == KeyboardInputResult.Cancel)
        {
            Complete(false);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!allowPermanentClose)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(CancelSession, DispatcherPriority.Send);
            return;
        }

        base.OnClosing(e);
    }

    private void Complete(bool isConfirmed)
    {
        if (!isSessionActive)
        {
            return;
        }

        IsConfirmed = isConfirmed;
        isSessionActive = false;
        Hide();
    }
}
