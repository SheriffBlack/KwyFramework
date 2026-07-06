using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Kwy.ComponentModel;
using Kwy.MVVM.Core;

namespace Kwy.UI.WPF.Components.PropertyGrid;

/// <summary>
/// UI binding model for one reflected property.
/// </summary>
public sealed class DynamicPropertyItem : BindableBase
{
    private readonly object source;
    private readonly PropertyInfo propertyInfo;
    private readonly PropertyInfo? unitPropertyInfo;
    private readonly Func<object, object?> getter;
    private readonly Action<object, object?>? setter;
    private readonly Action<object, object?>? unitSetter;
    private readonly Func<object, object?>? unitGetter;

    public DynamicPropertyItem(object source, PropertyMetadataItem metadata)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(metadata);

        propertyInfo = metadata.Property;
        getter = BuildGetter(propertyInfo);
        setter = metadata.IsReadOnly ? null : BuildSetter(propertyInfo);

        unitPropertyInfo = source.GetType().GetProperty(propertyInfo.Name + "Unit", BindingFlags.Instance | BindingFlags.Public);
        if (unitPropertyInfo is { CanRead: true })
        {
            unitGetter = BuildGetter(unitPropertyInfo);
            unitSetter = unitPropertyInfo.CanWrite ? BuildSetter(unitPropertyInfo) : null;
        }

        DisplayName = metadata.DisplayName;
        GroupName = metadata.Category;
        InputType = metadata.InputType;
        ItemsSource = metadata.ItemsSource;
        GroupWidth = metadata.GroupWidth;
        IsReadOnly = setter == null;
        IsInteger = IsIntegerType(propertyInfo.PropertyType);
        Minimum = metadata.Minimum;
        Maximum = metadata.Maximum;
        SmallChange = metadata.SmallChange ?? (IsInteger ? 1.0 : 0.1);
        DecimalPlaces = metadata.DecimalPlaces ?? (IsInteger ? 0 : 3);
    }

    public DynamicPropertyItem(object source, PropertyInfo propertyInfo)
        : this(source, ResolveMetadata(propertyInfo))
    {
    }

    public string GroupName { get; }

    public string DisplayName { get; }

    public InputType InputType { get; }

    public object? ItemsSource { get; }

    public bool IsReadOnly { get; }

    public double? GroupWidth { get; }

    public bool IsInteger { get; }

    public double? Minimum { get; }

    public double? Maximum { get; }

    public double SmallChange { get; }

    public int DecimalPlaces { get; }

    public object? Value
    {
        get => getter(source);
        set
        {
            if (setter == null)
            {
                return;
            }

            setter(source, ConvertValue(value, propertyInfo.PropertyType));
            RaisePropertyChanged();
        }
    }

    public object? UnitValue
    {
        get => unitGetter?.Invoke(source);
        set
        {
            if (unitPropertyInfo == null || unitSetter == null)
            {
                return;
            }

            unitSetter(source, ConvertValue(value, unitPropertyInfo.PropertyType));
            RaisePropertyChanged();
        }
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value == null)
        {
            return Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType
                ? null
                : Activator.CreateInstance(targetType);
        }

        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return Nullable.GetUnderlyingType(targetType) != null || targetType == typeof(string)
                ? null
                : Activator.CreateInstance(targetType);
        }

        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        if (effectiveType.IsEnum)
        {
            return value is string enumText
                ? Enum.Parse(effectiveType, enumText, ignoreCase: true)
                : Enum.ToObject(effectiveType, value);
        }

        if (effectiveType == typeof(Guid))
        {
            return value is Guid guid ? guid : Guid.Parse(value.ToString()!);
        }

        var converter = TypeDescriptor.GetConverter(effectiveType);
        if (converter.CanConvertFrom(value.GetType()))
        {
            return converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
        }

        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static bool IsIntegerType(Type type)
    {
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        return effectiveType == typeof(byte)
            || effectiveType == typeof(sbyte)
            || effectiveType == typeof(short)
            || effectiveType == typeof(ushort)
            || effectiveType == typeof(int)
            || effectiveType == typeof(uint)
            || effectiveType == typeof(long)
            || effectiveType == typeof(ulong);
    }

    private static PropertyMetadataItem ResolveMetadata(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        Type declaringType = propertyInfo.DeclaringType
            ?? throw new ArgumentException("The property must have a declaring type.", nameof(propertyInfo));

        return PropertyMetadataReader.GetProperties(declaringType)
            .First(item => string.Equals(item.Property.Name, propertyInfo.Name, StringComparison.Ordinal));
    }

    private static Func<object, object?> BuildGetter(PropertyInfo propertyInfo)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var typedInstance = Expression.Convert(instance, propertyInfo.DeclaringType!);
        var property = Expression.Property(typedInstance, propertyInfo);
        var result = Expression.Convert(property, typeof(object));

        return Expression.Lambda<Func<object, object?>>(result, instance).Compile();
    }

    private static Action<object, object?> BuildSetter(PropertyInfo propertyInfo)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var value = Expression.Parameter(typeof(object), "value");
        var typedInstance = Expression.Convert(instance, propertyInfo.DeclaringType!);
        var typedValue = Expression.Convert(value, propertyInfo.PropertyType);
        var property = Expression.Property(typedInstance, propertyInfo);
        var assign = Expression.Assign(property, typedValue);

        return Expression.Lambda<Action<object, object?>>(assign, instance, value).Compile();
    }
}
