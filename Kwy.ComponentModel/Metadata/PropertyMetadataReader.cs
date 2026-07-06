using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Kwy.ComponentModel;

/// <summary>
/// Reads and caches property metadata declared with System.ComponentModel and Kwy.ComponentModel attributes.
/// </summary>
public static class PropertyMetadataReader
{
    public const string DefaultCategory = "常规";

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyMetadataItem>> PropertyCache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PlcPointMetadataItem>> PlcPointCache = new();
    private static readonly ConcurrentDictionary<Type, string[]> EnumNameCache = new();
    private static readonly ConcurrentDictionary<Type, string[]> EnumDescriptionCache = new();

    public static IReadOnlyList<PropertyMetadataItem> GetProperties(object? source)
    {
        return source == null ? Array.Empty<PropertyMetadataItem>() : GetProperties(source.GetType());
    }

    public static IReadOnlyList<PropertyMetadataItem> GetProperties(Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        return PropertyCache.GetOrAdd(sourceType, ReadProperties);
    }

    public static IReadOnlyList<(PropertyInfo Property, string Category, string DisplayName)> GetCategorizedProperties(object? source)
    {
        return GetProperties(source)
            .Where(item => item.HasCategory)
            .Select(item => (item.Property, item.Category, item.DisplayName))
            .ToArray();
    }

    public static string[] GetEnumNames(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        Type effectiveType = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!effectiveType.IsEnum)
        {
            throw new ArgumentException("The specified type must be an enum type.", nameof(enumType));
        }

        return EnumNameCache.GetOrAdd(effectiveType, Enum.GetNames);
    }

    public static string[] GetEnumDescriptions<TEnum>()
        where TEnum : Enum
    {
        return GetEnumDescriptions(typeof(TEnum));
    }

    public static string[] GetEnumDescriptions(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        Type effectiveType = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!effectiveType.IsEnum)
        {
            throw new ArgumentException("The specified type must be an enum type.", nameof(enumType));
        }

        return EnumDescriptionCache.GetOrAdd(effectiveType, type => Enum.GetValues(type)
            .Cast<Enum>()
            .Select(GetEnumDescription)
            .ToArray());
    }

    public static string GetEnumDescription(Enum enumValue)
    {
        ArgumentNullException.ThrowIfNull(enumValue);

        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        return fieldInfo?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? enumValue.ToString();
    }

    public static IReadOnlyList<PlcPointMetadataItem> GetPlcPoints<TEnum>()
        where TEnum : struct, Enum
    {
        return GetPlcPoints(typeof(TEnum));
    }

    public static IReadOnlyList<PlcPointMetadataItem> GetPlcPoints(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        Type effectiveType = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!effectiveType.IsEnum)
        {
            throw new ArgumentException("The specified type must be an enum type.", nameof(enumType));
        }

        return PlcPointCache.GetOrAdd(effectiveType, ReadPlcPoints);
    }

    private static IReadOnlyList<PropertyMetadataItem> ReadProperties(Type sourceType)
    {
        return sourceType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(CreateMetadata)
            .ToArray();
    }

    private static IReadOnlyList<PlcPointMetadataItem> ReadPlcPoints(Type enumType)
    {
        return Enum.GetValues(enumType)
            .Cast<Enum>()
            .Select(ReadPlcPoint)
            .ToArray();
    }

    private static PlcPointMetadataItem ReadPlcPoint(Enum enumValue)
    {
        FieldInfo field = enumValue.GetType().GetField(enumValue.ToString())
            ?? throw new InvalidOperationException($"Cannot find enum field: {enumValue}.");

        var plcPoint = field.GetCustomAttribute<PlcPointAttribute>()
            ?? throw new InvalidOperationException($"Enum field {field.Name} missing {nameof(PlcPointAttribute)}.");
        string displayName = field.GetCustomAttribute<DescriptionAttribute>()?.Description ?? field.Name;

        return new PlcPointMetadataItem(
            enumValue,
            field.Name,
            displayName,
            plcPoint.Address,
            plcPoint.DataType,
            plcPoint.IsReadOnly);
    }

    private static PropertyMetadataItem CreateMetadata(PropertyInfo property)
    {
        var categoryAttribute = property.GetCustomAttribute<CategoryAttribute>();
        var browsableAttribute = property.GetCustomAttribute<BrowsableAttribute>();
        var readOnlyAttribute = property.GetCustomAttribute<ReadOnlyAttribute>();
        var groupWidthAttribute = property.GetCustomAttribute<GroupWidthAttribute>();
        var numberRangeAttribute = property.GetCustomAttribute<NumberRangeAttribute>();

        bool isReadableProperty = property.CanRead && property.GetIndexParameters().Length == 0;
        bool isBrowsable = isReadableProperty && browsableAttribute?.Browsable != false;
        bool isReadOnly = readOnlyAttribute?.IsReadOnly == true || !property.CanWrite;

        return new PropertyMetadataItem(
            property,
            categoryAttribute?.Category ?? DefaultCategory,
            property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name,
            ResolveInputType(property, isReadOnly),
            ResolveItemsSource(property),
            groupWidthAttribute?.WidthRatio > 0 ? groupWidthAttribute.WidthRatio : null,
            isBrowsable,
            isReadOnly,
            categoryAttribute != null,
            numberRangeAttribute?.Minimum,
            numberRangeAttribute?.Maximum,
            numberRangeAttribute?.SmallChange is > 0 ? numberRangeAttribute.SmallChange : null,
            numberRangeAttribute?.DecimalPlaces is >= 0 ? numberRangeAttribute.DecimalPlaces : null);
    }

    private static InputType ResolveInputType(PropertyInfo property, bool isReadOnly)
    {
        var attribute = property.GetCustomAttribute<InputTypeAttribute>();
        if (attribute != null)
        {
            return attribute.Type;
        }

        if (isReadOnly)
        {
            return InputType.TextBlock;
        }

        Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(bool))
        {
            return InputType.ToggleButton;
        }

        if (propertyType.IsEnum)
        {
            return InputType.ComboBox;
        }

        if (propertyType == typeof(DateTime) || propertyType == typeof(DateTimeOffset))
        {
            return InputType.DatePicker;
        }

        if (IsNumericType(propertyType))
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

    private static object? ResolveItemsSource(PropertyInfo property)
    {
        var itemsSource = property.GetCustomAttribute<ItemsSourceAttribute>();
        if (itemsSource != null)
        {
            return itemsSource.Items;
        }

        Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return propertyType.IsEnum ? GetEnumNames(propertyType) : null;
    }
}
