using System.Reflection;

namespace Kwy.ComponentModel;

/// <summary>
/// Describes a public property and the UI metadata declared on it.
/// </summary>
public sealed record PropertyMetadataItem(
    PropertyInfo Property,
    string Category,
    string DisplayName,
    InputType InputType,
    object? ItemsSource,
    double? GroupWidth,
    bool IsBrowsable,
    bool IsReadOnly,
    bool HasCategory,
    double? Minimum,
    double? Maximum,
    double? SmallChange,
    int? DecimalPlaces);
