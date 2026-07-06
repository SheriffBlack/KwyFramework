using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
    private bool isCoercingValue;

    static KwyNumberBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KwyNumberBox),
            new FrameworkPropertyMetadata(typeof(KwyNumberBox)));
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(object),
            typeof(KwyNumberBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

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
            new PropertyMetadata(1.0));

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
            new PropertyMetadata(false, OnValueChanged));

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
            new PropertyMetadata(null, OnValueChanged));

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
            new PropertyMetadata(null, OnValueChanged));

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
            new PropertyMetadata(3, OnValueChanged));

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

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KwyNumberBox)d).CoerceCurrentValue();

    private void CoerceCurrentValue()
    {
        if (isCoercingValue)
        {
            UpdateTextFromValue();
            return;
        }

        if (TryReadDouble(Value, out double number))
        {
            double normalized = NormalizeNumber(number);
            if (Math.Abs(normalized - number) > double.Epsilon)
            {
                isCoercingValue = true;
                try
                {
                    Value = normalized;
                }
                finally
                {
                    isCoercingValue = false;
                }
            }
        }

        UpdateTextFromValue();
    }

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
            CommitText();
            e.Handled = true;
        }
    }

    private void Step(int direction)
    {
        if (IsReadOnly || !IsEnabled)
        {
            return;
        }

        double current = TryReadDouble(Value, out double value) ? value : 0;
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
        CommitText();
        UpdateTextFromValue();
    }

    private void CommitText()
    {
        if (textBox == null)
        {
            return;
        }

        string text = textBox.Text;
        if (string.IsNullOrWhiteSpace(text) || text == "-" || text == "." || text == "-.")
        {
            Value = null;
            return;
        }

        if (!TryParseDouble(text, out double parsed))
        {
            return;
        }

        Value = NormalizeNumber(parsed);
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

    private string FormatValue(object? value)
    {
        if (!TryReadDouble(value, out double number))
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

    private static bool TryReadDouble(object? value, out double number)
    {
        number = 0;
        if (value == null)
        {
            return false;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetDouble(out number);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString();
            }
        }

        return value switch
        {
            null => false,
            byte v => Set(v, out number),
            sbyte v => Set(v, out number),
            short v => Set(v, out number),
            ushort v => Set(v, out number),
            int v => Set(v, out number),
            uint v => Set(v, out number),
            long v => Set(v, out number),
            ulong v => Set(v, out number),
            float v => Set(v, out number),
            double v => Set(v, out number),
            decimal v => Set((double)v, out number),
            string text => TryParseDouble(text, out number),
            { } other => TryParseDouble(other.ToString() ?? string.Empty, out number)
        };
    }

    private static bool Set(double value, out double number)
    {
        number = value;
        return true;
    }

    private static bool TryParseDouble(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
