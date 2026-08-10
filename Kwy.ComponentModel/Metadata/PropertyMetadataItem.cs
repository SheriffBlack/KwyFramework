using System.Reflection;

namespace Kwy.ComponentModel;

/// <summary>
/// Describes a public property and the UI metadata declared on it.
/// </summary>
public sealed record PropertyMetadataItem(
    PropertyInfo Property,
    string Category,
    string? CategoryKey,
    string DisplayName,
    string? DisplayNameKey,
    InputType InputType,
    object? ItemsSource,
    string? ItemsSourceProviderName,
    double? GroupWidth,
    string? InlineGroup,
    double? EditorWidth,
    bool IsBrowsable,
    bool IsReadOnly,
    bool HasCategory,
    double? Minimum,
    double? Maximum,
    double? SmallChange,
    int? DecimalPlaces);