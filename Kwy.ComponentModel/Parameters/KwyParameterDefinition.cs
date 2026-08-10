namespace Kwy.ComponentModel;

/// <summary>
/// Describes an editable parameter independently from any specific UI framework.
/// </summary>
public sealed class KwyParameterDefinition
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Category { get; init; } = PropertyMetadataReader.DefaultCategory;

    public Type ValueType { get; init; } = typeof(string);

    public object? DefaultValue { get; init; }

    public string? Description { get; init; }

    public InputType InputType { get; init; } = InputType.TextBox;

    public object? ItemsSource { get; init; }

    public double? GroupWidth { get; init; }

    public string? InlineGroup { get; init; }

    public double? EditorWidth { get; init; }

    public bool IsRequired { get; init; }

    public bool IsReadOnly { get; init; }

    public bool IsBrowsable { get; init; } = true;

    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public double? SmallChange { get; init; }

    public int? DecimalPlaces { get; init; }

    public static KwyParameterDefinition Create<T>(
        string key,
        string? displayName = null,
        T? defaultValue = default,
        string? category = null,
        string? description = null,
        InputType? inputType = null,
        object? itemsSource = null,
        double? groupWidth = null,
        string? inlineGroup = null,
        double? editorWidth = null,
        bool isRequired = false,
        bool isReadOnly = false,
        bool isBrowsable = true,
        double? minimum = null,
        double? maximum = null,
        double? smallChange = null,
        int? decimalPlaces = null)
        => new()
        {
            Key = key,
            DisplayName = displayName ?? key,
            Category = category ?? PropertyMetadataReader.DefaultCategory,
            ValueType = typeof(T),
            DefaultValue = defaultValue,
            Description = description,
            InputType = inputType ?? ResolveDefaultInputType(typeof(T), isReadOnly),
            ItemsSource = itemsSource,
            GroupWidth = groupWidth,
            InlineGroup = inlineGroup,
            EditorWidth = editorWidth,
            IsRequired = isRequired,
            IsReadOnly = isReadOnly,
            IsBrowsable = isBrowsable,
            Minimum = minimum,
            Maximum = maximum,
            SmallChange = smallChange,
            DecimalPlaces = decimalPlaces
        };

    internal static InputType ResolveDefaultInputType(Type valueType, bool isReadOnly)
    {
        Type effectiveType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (isReadOnly)
        {
            return InputType.TextBlock;
        }

        if (effectiveType == typeof(bool))
        {
            return InputType.ToggleButton;
        }

        if (effectiveType.IsEnum)
        {
            return InputType.ComboBox;
        }

        if (effectiveType == typeof(DateTime) || effectiveType == typeof(DateTimeOffset))
        {
            return InputType.DatePicker;
        }

        if (IsNumericType(effectiveType))
        {
            return InputType.NumberBox;
        }

        return InputType.TextBox;
    }

    private static bool IsNumericType(Type type)
        => type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
}
