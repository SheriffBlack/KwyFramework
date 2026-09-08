using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Kwy.UI.WPF.Controls.NumberInput;

/// <summary>
/// Specifies how an integral value is displayed and parsed.
/// </summary>
public enum NumericBase
{
    Decimal,
    Hexadecimal
}

/// <summary>
/// Converts integral values using an explicit numeric base.
/// </summary>
public sealed class NumericBaseConverter : IValueConverter
{
    public NumericBase Base { get; set; } = NumericBase.Decimal;

    public bool IncludeHexPrefix { get; set; } = true;

    public int MinimumDigits { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return string.Empty;
        }

        try
        {
            long number = System.Convert.ToInt64(value, culture);
            if (Base == NumericBase.Decimal)
            {
                return number.ToString(culture);
            }

            string digits = number.ToString($"X{Math.Max(0, MinimumDigits)}", CultureInfo.InvariantCulture);
            return IncludeHexPrefix ? $"0x{digits}" : digits;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? text = value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return Binding.DoNothing;
        }

        bool hasHexPrefix = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        string digits = hasHexPrefix ? text[2..] : text;
        NumberStyles styles = Base == NumericBase.Hexadecimal || hasHexPrefix
            ? NumberStyles.AllowHexSpecifier
            : NumberStyles.Integer;

        if (!long.TryParse(digits, styles, styles == NumberStyles.AllowHexSpecifier ? CultureInfo.InvariantCulture : culture, out long number))
        {
            return DependencyProperty.UnsetValue;
        }

        Type destinationType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            return System.Convert.ChangeType(number, destinationType, culture);
        }
        catch (Exception exception) when (exception is InvalidCastException or OverflowException)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
