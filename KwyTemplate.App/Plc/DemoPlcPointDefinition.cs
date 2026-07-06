namespace KwyTemplate.App.Plc;

public sealed record DemoPlcPointDefinition(
    DemoPlcPoint Point,
    string Address,
    string DisplayName,
    Type DataType,
    bool IsReadOnly);
