using Kwy.ComponentModel;

namespace KwyTemplate.App.Plc;

public static class DemoPlcPoints
{
    private static readonly Lazy<IReadOnlyList<DemoPlcPointDefinition>> definitions = new(ReadDefinitions);

    public static IReadOnlyList<DemoPlcPointDefinition> All => definitions.Value;

    public static DemoPlcPointDefinition Get(DemoPlcPoint point)
        => definitions.Value.First(definition => definition.Point == point);

    public static IEnumerable<DemoPlcPointDefinition> CassetteSwitches()
        => definitions.Value.Where(definition => definition.Point is
            DemoPlcPoint.BadProductBoxLock1Manual or
            DemoPlcPoint.BadProductBoxLock2Manual or
            DemoPlcPoint.BadProductBoxLock3Manual or
            DemoPlcPoint.BadProductBoxLock4Manual or
            DemoPlcPoint.BadProductBoxLock5Manual);

    public static IEnumerable<DemoPlcPointDefinition> AlarmMonitors()
        => definitions.Value.Where(definition => definition.Point is
            DemoPlcPoint.WearingPartCountReachedAlarm or
            DemoPlcPoint.AirPressureDetectionAlarm);

    private static IReadOnlyList<DemoPlcPointDefinition> ReadDefinitions()
    {
        return PropertyMetadataReader.GetPlcPoints<DemoPlcPoint>()
            .Select(ToDefinition)
            .ToArray();
    }

    private static DemoPlcPointDefinition ToDefinition(PlcPointMetadataItem metadata)
    {
        return new DemoPlcPointDefinition(
            (DemoPlcPoint)metadata.Value,
            metadata.Address,
            metadata.DisplayName,
            metadata.DataType,
            metadata.IsReadOnly);
    }
}
