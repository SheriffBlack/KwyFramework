using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Application = System.Windows.Application;
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
    private readonly object? staticItemsSource;
    private readonly Func<object, object?>? itemsSourceProviderGetter;
    private readonly string displayNameFallback;
    private readonly string? displayNameKey;
    private readonly string groupNameFallback;
    private readonly string? groupNameKey;

    internal event EventHandler? ValueChanged;

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
        displayNameFallback = metadata.DisplayName;
        displayNameKey = metadata.DisplayNameKey;
        groupNameFallback = metadata.Category;
        groupNameKey = metadata.CategoryKey;
        InputType = metadata.InputType;
        staticItemsSource = metadata.ItemsSource;
        itemsSourceProviderGetter = BuildItemsSourceProviderGetter(source, metadata.ItemsSourceProviderName);
        GroupWidth = metadata.GroupWidth;
        InlineGroup = metadata.InlineGroup;
        EditorWidth = metadata.EditorWidth is > 0 ? metadata.EditorWidth.Value : 180.0;
        IsReadOnly = setter == null;
        IsInteger = IsIntegerType(propertyInfo.PropertyType);
        Minimum = metadata.Minimum;
        Maximum = metadata.Maximum;
        SmallChange = metadata.SmallChange ?? (IsInteger ? 1.0 : 0.1);
        DecimalPlaces = metadata.DecimalPlaces ?? (IsInteger ? 0 : 3);
        RefreshesPropertyGrid = propertyInfo.GetCustomAttribute<RefreshPropertyGridAttribute>() != null;
    }

    public DynamicPropertyItem(object source, PropertyInfo propertyInfo)
        : this(source, ResolveMetadata(propertyInfo))
    {
    }

    public string GroupName => ResolveResource(groupNameKey, groupNameFallback);

    public string DisplayName => ResolveResource(displayNameKey, displayNameFallback);

    public InputType InputType { get; }

    public object? ItemsSource => itemsSourceProviderGetter?.Invoke(source) ?? staticItemsSource;

    public bool IsReadOnly { get; }

    public double? GroupWidth { get; }

    public string? InlineGroup { get; }

    public double EditorWidth { get; }

    public bool IsInteger { get; }

    public double? Minimum { get; }

    public double? Maximum { get; }

    public double SmallChange { get; }

    public int DecimalPlaces { get; }

    internal bool RefreshesPropertyGrid { get; }

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
            ValueChanged?.Invoke(this, EventArgs.Empty);
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
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void RefreshLocalization()
    {
        RaisePropertyChanged(nameof(GroupName));
        RaisePropertyChanged(nameof(DisplayName));
    }

    internal void RefreshDynamicItemsSource()
    {
        if (itemsSourceProviderGetter == null)
        {
            return;
        }

        CoerceUnitValueToItemsSource();
        RaisePropertyChanged(nameof(ItemsSource));
    }

    private void CoerceUnitValueToItemsSource()
    {
        if (unitPropertyInfo == null || unitSetter == null)
        {
            return;
        }

        List<object?> items = EnumerateItemsSource(ItemsSource).ToList();
        if (items.Count == 0)
        {
            return;
        }

        object? current = UnitValue;
        if (items.Any(item => string.Equals(item?.ToString(), current?.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        UnitValue = items[0];
    }

    private static string ResolveResource(string? key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        object? value = Application.Current?.TryFindResource(key);
        string? text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private static IEnumerable<object?> EnumerateItemsSource(object? itemsSource)
    {
        if (itemsSource is null)
        {
            yield break;
        }

        if (itemsSource is string text)
        {
            yield return text;
            yield break;
        }

        if (itemsSource is System.Collections.IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return item;
            }
        }
    }

    private static Func<object, object?>? BuildItemsSourceProviderGetter(object source, string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        Type sourceType = source.GetType();
        PropertyInfo? providerProperty = sourceType.GetProperty(providerName, BindingFlags.Instance | BindingFlags.Public);
        if (providerProperty is { CanRead: true } && providerProperty.GetIndexParameters().Length == 0)
        {
            Func<object, object?> getter = BuildGetter(providerProperty);
            return instance => getter(instance);
        }

        MethodInfo? providerMethod = sourceType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => string.Equals(method.Name, providerName, StringComparison.Ordinal)
                && method.GetParameters().Length == 0);
        if (providerMethod != null)
        {
            return instance => providerMethod.Invoke(instance, null);
        }

        throw new InvalidOperationException($"Cannot find items source provider '{providerName}' on {sourceType.FullName}.");
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
