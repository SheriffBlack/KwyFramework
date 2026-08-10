namespace KwyTemplate.Flow.Models;

public sealed record MachinePlcPointDefinition(
    string Key,
    string Address,
    string DisplayName,
    Type DataType,
    bool IsReadOnly);
