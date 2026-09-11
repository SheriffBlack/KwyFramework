using Kwy.UI.WPF.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace Kwy.UI.WPF.Input;

/// <summary>
/// Opens an application-local soft keyboard for an enabled <see cref="TextBox"/>.
/// </summary>
public static class SoftKeyboardService
{
    private static readonly DependencyProperty SessionProperty = DependencyProperty.RegisterAttached(
        "Session",
        typeof(KeyboardSession),
        typeof(SoftKeyboardService));

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SoftKeyboardService),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty LayoutProperty = DependencyProperty.RegisterAttached(
        "Layout",
        typeof(KeyboardLayout),
        typeof(SoftKeyboardService),
        new PropertyMetadata(KeyboardLayout.Qwerty),
        static value => value is KeyboardLayout layout && Enum.IsDefined(layout));

    public static void SetLayout(DependencyObject element, KeyboardLayout value) => element.SetValue(LayoutProperty, value);
    public static KeyboardLayout GetLayout(DependencyObject element) => (KeyboardLayout)element.GetValue(LayoutProperty);

    public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
        "Mode",
        typeof(SoftKeyboardMode),
        typeof(SoftKeyboardService),
        new PropertyMetadata(SoftKeyboardMode.Full),
        static value => value is SoftKeyboardMode mode && Enum.IsDefined(mode));

    public static void SetMode(DependencyObject element, SoftKeyboardMode value) => element.SetValue(ModeProperty, value);
    public static SoftKeyboardMode GetMode(DependencyObject element) => (SoftKeyboardMode)element.GetValue(ModeProperty);

    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached(
        "AllowNegative", typeof(bool), typeof(SoftKeyboardService), new PropertyMetadata(true));

    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);

    public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.RegisterAttached(
        "DecimalPlaces", typeof(int?), typeof(SoftKeyboardService), new PropertyMetadata(null),
        static value => value is null || (value is int places && places is >= 0 and <= 15));

    public static void SetDecimalPlaces(DependencyObject element, int? value) => element.SetValue(DecimalPlacesProperty, value);
    public static int? GetDecimalPlaces(DependencyObject element) => (int?)element.GetValue(DecimalPlacesProperty);

    public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached(
        "Width",
        typeof(double),
        typeof(SoftKeyboardService),
        new PropertyMetadata(760d),
        static value => value is double width && double.IsFinite(width) && width > 0);

    public static void SetWidth(DependencyObject element, double value) => element.SetValue(WidthProperty, value);
    public static double GetWidth(DependencyObject element) => (double)element.GetValue(WidthProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || d is not (TextBox or KwyNumberBox))
        {
            throw new InvalidOperationException("SoftKeyboardService can only be attached to a TextBox or KwyNumberBox.");
        }

        Detach(element);
        if ((bool)e.NewValue)
        {
            element.GotKeyboardFocus += OnGotKeyboardFocus;
            element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            element.Unloaded += OnElementUnloaded;
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Open(element);
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.Dispatcher.BeginInvoke(() => Open(element));
        }
    }

    private static void Open(FrameworkElement element)
    {
        TextBox? textBox = element switch
        {
            TextBox direct => direct,
            KwyNumberBox numberBox => numberBox.Editor,
            _ => null
        };
        bool isReadOnly = element is TextBox directTextBox ? directTextBox.IsReadOnly : ((KwyNumberBox)element).IsReadOnly;
        if (!GetIsEnabled(element) || textBox == null || isReadOnly || !element.IsEnabled)
        {
            return;
        }

        if (element.GetValue(SessionProperty) is KeyboardSession existing)
        {
            existing.Popup.IsOpen = true;
            return;
        }

        SoftKeyboardMode mode = element is KwyNumberBox number
            ? number.IsInteger ? SoftKeyboardMode.Integer : SoftKeyboardMode.Numeric
            : GetMode(element);
        bool allowNegative = element is KwyNumberBox numberInput
            ? numberInput.Minimum is null or < 0
            : GetAllowNegative(element);
        var numericOptions = mode == SoftKeyboardMode.Full
            ? null
            : new NumericKeyboardOptions(
                mode == SoftKeyboardMode.Numeric,
                allowNegative,
                element is KwyNumberBox numeric ? numeric.DecimalPlaces : GetDecimalPlaces(element),
                element is KwyNumberBox bounded ? bounded.Minimum : null,
                element is KwyNumberBox boundedMaximum ? boundedMaximum.Maximum : null);
        var keyboard = new KwyKeyboard
        {
            Focusable = false,
            KeyboardLayout = GetLayout(element),
            Mode = mode,
            AllowNegative = allowNegative,
            Width = GetWidth(element)
        };
        var popup = new Popup
        {
            AllowsTransparency = true,
            Child = keyboard,
            Focusable = false,
            Placement = PlacementMode.Bottom,
            PlacementTarget = element,
            StaysOpen = false
        };
        var session = new KeyboardSession(textBox, keyboard, popup, numericOptions);
        keyboard.KeyInvoked += session.OnKeyInvoked;
        popup.Closed += session.OnClosed;
        element.SetValue(SessionProperty, session);
        popup.IsOpen = true;
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Detach(element);
        }
    }

    private static void Detach(FrameworkElement element)
    {
        element.GotKeyboardFocus -= OnGotKeyboardFocus;
        element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        element.Unloaded -= OnElementUnloaded;

        if (element.GetValue(SessionProperty) is KeyboardSession session)
        {
            session.Dispose();
            element.ClearValue(SessionProperty);
        }
    }

    private sealed class KeyboardSession : IDisposable
    {
        private readonly TextBoxKeyboardInputAdapter target;
        private readonly TextBox textBox;
        private readonly KwyKeyboard keyboard;
        private string originalText;

        public KeyboardSession(TextBox textBox, KwyKeyboard keyboard, Popup popup, NumericKeyboardOptions? numericOptions)
        {
            this.textBox = textBox;
            this.keyboard = keyboard;
            Popup = popup;
            target = new TextBoxKeyboardInputAdapter(textBox, numericOptions);
            originalText = textBox.Text;
        }

        public Popup Popup { get; }

        public void OnKeyInvoked(object? sender, KeyboardKeyInvokedEventArgs e)
        {
            KeyboardInputResult result = target.HandleKey(e, keyboard.KeyboardLayout);
            e.Handled = true;
            if (result == KeyboardInputResult.Commit)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                Popup.IsOpen = false;
            }
            else if (result == KeyboardInputResult.Cancel)
            {
                textBox.Text = originalText;
                Popup.IsOpen = false;
            }
        }

        public void OnClosed(object? sender, EventArgs e)
        {
            originalText = textBox.Text;
        }

        public void Dispose()
        {
            Popup.IsOpen = false;
            keyboard.KeyInvoked -= OnKeyInvoked;
            Popup.Closed -= OnClosed;
            Popup.Child = null;
            Popup.PlacementTarget = null;
        }
    }
}
