using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

[TemplatePart(Name = PartTextBox, Type = typeof(TextBox))]
[TemplatePart(Name = PartIncreaseButton, Type = typeof(RepeatButton))]
[TemplatePart(Name = PartDecreaseButton, Type = typeof(RepeatButton))]
public class KwyNumberBox : Control
{
    private const string PartTextBox = "PART_TextBox";
    private const string PartIncreaseButton = "PART_IncreaseButton";
    private const string PartDecreaseButton = "PART_DecreaseButton";

    private TextBox? textBox;
    private RepeatButton? increaseButton;
    private RepeatButton? decreaseButton;
    private bool isUpdatingText;

    internal TextBox? Editor => textBox;

    static KwyNumberBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KwyNumberBox),
            new FrameworkPropertyMetadata(typeof(KwyNumberBox)));
    }

    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double?),
            typeof(KwyNumberBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                CoerceValue),
            IsNullableFiniteNumber);

    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(
            nameof(SmallChange),
            typeof(double),
            typeof(KwyNumberBox),
            new PropertyMetadata(1.0),
            IsPositiveFiniteNumber);

    public bool IsInteger
    {
        get => (bool)GetValue(IsIntegerProperty);
        set => SetValue(IsIntegerProperty, value);
    }

    public static readonly DependencyProperty IsIntegerProperty =
        DependencyProperty.Register(
            nameof(IsInteger),
            typeof(bool),
            typeof(KwyNumberBox),
            new PropertyMetadata(false, OnNumberFormatChanged));

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(KwyNumberBox),
            new PropertyMetadata(false));

    public double? Minimum
    {
        get => (double?)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(double?),
            typeof(KwyNumberBox),
            new PropertyMetadata(null, OnMinimumChanged),
            IsNullableFiniteNumber);

    public double? Maximum
    {
        get => (double?)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double?),
            typeof(KwyNumberBox),
            new PropertyMetadata(null, OnMaximumChanged, CoerceMaximum),
            IsNullableFiniteNumber);

    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(
            nameof(DecimalPlaces),
            typeof(int),
            typeof(KwyNumberBox),
            new PropertyMetadata(3, OnNumberFormatChanged),
            value => value is int places && places is >= 0 and <= 15);

    public override void OnApplyTemplate()
    {
        DetachTemplateParts();
        base.OnApplyTemplate();

        textBox = GetTemplateChild(PartTextBox) as TextBox;
        increaseButton = GetTemplateChild(PartIncreaseButton) as RepeatButton;
        decreaseButton = GetTemplateChild(PartDecreaseButton) as RepeatButton;

        if (textBox != null)
        {
            textBox.TextChanged += OnTextChanged;
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.LostKeyboardFocus += OnLostKeyboardFocus;
            DataObject.AddPastingHandler(textBox, OnPaste);
        }

        if (increaseButton != null)
        {
            increaseButton.Click += OnIncreaseClick;
        }

        if (decreaseButton != null)
        {
            decreaseButton.Click += OnDecreaseClick;
        }

        UpdateTextFromValue();
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => new KwyNumberBoxAutomationPeer(this);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((KwyNumberBox)d).UpdateTextFromValue();
    }

    private static void OnNumberFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(ValueProperty);
        ((KwyNumberBox)d).UpdateTextFromValue();
    }

    private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(MaximumProperty);
        d.CoerceValue(ValueProperty);
        ((KwyNumberBox)d).UpdateTextFromValue();
    }

    private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.CoerceValue(ValueProperty);
        ((KwyNumberBox)d).UpdateTextFromValue();
    }

    private static object? CoerceValue(DependencyObject d, object? baseValue)
        => baseValue is double number ? ((KwyNumberBox)d).NormalizeNumber(number) : null;

    private static object? CoerceMaximum(DependencyObject d, object? baseValue)
    {
        if (baseValue is not double maximum || ((KwyNumberBox)d).Minimum is not double minimum)
        {
            return baseValue;
        }

        return Math.Max(minimum, maximum);
    }

    private static bool IsNullableFiniteNumber(object? value)
        => value is null || value is double number && double.IsFinite(number);

    private static bool IsPositiveFiniteNumber(object value)
        => value is double number && double.IsFinite(number) && number > 0d;

    private void DetachTemplateParts()
    {
        if (textBox != null)
        {
            textBox.TextChanged -= OnTextChanged;
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
            DataObject.RemovePastingHandler(textBox, OnPaste);
        }

        if (increaseButton != null)
        {
            increaseButton.Click -= OnIncreaseClick;
        }

        if (decreaseButton != null)
        {
            decreaseButton.Click -= OnDecreaseClick;
        }
    }

    private void OnIncreaseClick(object sender, RoutedEventArgs e)
        => Step(+1);

    private void OnDecreaseClick(object sender, RoutedEventArgs e)
        => Step(-1);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            Step(+1);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            Step(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            FinalizeText();
            e.Handled = true;
        }
    }

    private void Step(int direction)
    {
        if (IsReadOnly || !IsEnabled)
        {
            return;
        }

        double current = Value ?? 0;
        double next = current + direction * SmallChange;
        Value = NormalizeNumber(next);
        UpdateTextFromValue();
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (textBox == null)
        {
            return;
        }

        string candidate = BuildCandidateText(e.Text);
        e.Handled = !IsValidEditingText(candidate);
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        string pasted = e.DataObject.GetData(DataFormats.Text)?.ToString() ?? string.Empty;
        if (!IsValidEditingText(BuildCandidateText(pasted)))
        {
            e.CancelCommand();
        }
    }

    private string BuildCandidateText(string insertText)
    {
        if (textBox == null)
        {
            return insertText;
        }

        string text = textBox.Text;
        int start = textBox.SelectionStart;
        int length = textBox.SelectionLength;
        return text.Remove(start, length).Insert(start, insertText);
    }

    private bool IsValidEditingText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "-" || (!IsInteger && text == ".") || (!IsInteger && text == "-."))
        {
            return true;
        }

        NumberStyles styles = IsInteger ? NumberStyles.Integer : NumberStyles.Float;
        return double.TryParse(text, styles, CultureInfo.CurrentCulture, out _)
            || double.TryParse(text, styles, CultureInfo.InvariantCulture, out _);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (isUpdatingText)
        {
            return;
        }

        CommitText();
    }

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        FinalizeText();
        UpdateTextFromValue();
    }

    private void FinalizeText()
    {
        if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
        {
            Value = null;
            return;
        }

        CommitText();
    }

    private void CommitText()
    {
        if (textBox == null)
        {
            return;
        }

        string text = textBox.Text;
        if (IsIntermediateEditingText(text))
        {
            return;
        }

        if (!TryParseDouble(text, out double parsed))
        {
            return;
        }

        Value = NormalizeNumber(parsed);
    }

    private bool IsIntermediateEditingText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "-")
        {
            return true;
        }

        string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        return !IsInteger && (text == separator || text == $"-{separator}" || text.EndsWith(separator, StringComparison.Ordinal));
    }

    private void UpdateTextFromValue()
    {
        if (textBox == null)
        {
            return;
        }

        string text = FormatValue(Value);
        if (textBox.Text == text)
        {
            return;
        }

        isUpdatingText = true;
        try
        {
            int caret = textBox.CaretIndex;
            textBox.Text = text;
            textBox.CaretIndex = Math.Min(caret, textBox.Text.Length);
        }
        finally
        {
            isUpdatingText = false;
        }
    }

    private string FormatValue(double? value)
    {
        if (value is not double number)
        {
            return string.Empty;
        }

        number = NormalizeNumber(number);
        return IsInteger
            ? Math.Round(number).ToString("0", CultureInfo.CurrentCulture)
            : number.ToString("0." + new string('#', Math.Max(0, DecimalPlaces)), CultureInfo.CurrentCulture);
    }

    private double NormalizeNumber(double value)
    {
        double number = IsInteger ? Math.Round(value) : value;
        if (Minimum is double min && number < min)
        {
            number = min;
        }

        if (Maximum is double max && number > max)
        {
            number = max;
        }

        return IsInteger ? Math.Round(number) : number;
    }

    private static bool TryParseDouble(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
