using System.ComponentModel;
using System.Reflection;

namespace Kwy.ComponentModel;

/// <summary>
/// Converts CLR property metadata into generic Kwy parameter definitions.
/// </summary>
public static class KwyParameterMetadataReader
{
    public static IReadOnlyList<KwyParameterDefinition> GetParameters(object? source)
    {
        if (source == null)
        {
            return Array.Empty<KwyParameterDefinition>();
        }

        return PropertyMetadataReader
            .GetProperties(source)
            .Where(item => item.IsBrowsable)
            .Select(item => CreateParameter(item, source))
            .ToArray();
    }

    public static IReadOnlyList<KwyParameterDefinition> GetParameters(Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);

        return PropertyMetadataReader
            .GetProperties(sourceType)
            .Where(item => item.IsBrowsable)
            .Select(item => CreateParameter(item, null))
            .ToArray();
    }

    public static KwyParameterDefinition CreateParameter(PropertyMetadataItem metadata, object? source = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new KwyParameterDefinition
        {
            Key = metadata.Property.Name,
            DisplayName = metadata.DisplayName,
            Category = metadata.Category,
            ValueType = metadata.Property.PropertyType,
            DefaultValue = TryGetValue(metadata.Property, source),
            Description = metadata.Property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            InputType = metadata.InputType,
            ItemsSource = metadata.ItemsSource,
            GroupWidth = metadata.GroupWidth,
            IsReadOnly = metadata.IsReadOnly,
            IsBrowsable = metadata.IsBrowsable,
            Minimum = metadata.Minimum,
            Maximum = metadata.Maximum,
            SmallChange = metadata.SmallChange,
            DecimalPlaces = metadata.DecimalPlaces
        };
    }

    private static object? TryGetValue(PropertyInfo property, object? source)
    {
        if (source == null || !property.CanRead || property.GetIndexParameters().Length != 0)
        {
            return null;
        }

        try
        {
            return property.GetValue(source);
        }
        catch
        {
            return null;
        }
    }
}
