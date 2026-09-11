namespace Kwy.UI.WPF.Input.Keyboard;

public sealed record NumericKeyboardOptions(
    bool AllowDecimal = true,
    bool AllowNegative = true,
    int? DecimalPlaces = null,
    double? Minimum = null,
    double? Maximum = null);
