namespace Kwy.ComponentModel;

/// <summary>
/// Describes a PLC point declared on an enum field.
/// </summary>
public sealed record PlcPointMetadataItem(
    Enum Value,
    string Name,
    string DisplayName,
    string Address,
    Type DataType,
    bool IsReadOnly);
