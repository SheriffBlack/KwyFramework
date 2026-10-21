using Kwy.Device.IoCard.Abstractions;

namespace Kwy.Device.IoCard.Core;

/// <summary>模拟量 IO 点位定义目录；在构造时校验稳定 ID 和物理通道唯一性。</summary>
public sealed class AnalogIoPointDefinitionProvider : IAnalogIoPointDefinitionProvider
{
    private readonly Dictionary<string, AnalogIoPointDefinition> byId;
    private readonly IReadOnlyCollection<AnalogIoPointDefinition> inputs;
    private readonly IReadOnlyCollection<AnalogIoPointDefinition> outputs;

    public AnalogIoPointDefinitionProvider(IEnumerable<AnalogIoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        byId = new Dictionary<string, AnalogIoPointDefinition>(StringComparer.OrdinalIgnoreCase);
        var physicalChannels = new HashSet<PhysicalChannel>(PhysicalChannelComparer.Instance);
        var inputItems = new List<AnalogIoPointDefinition>();
        var outputItems = new List<AnalogIoPointDefinition>();

        foreach (AnalogIoPointDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!byId.TryAdd(definition.Id, definition))
                throw new ArgumentException($"重复的模拟量 IO 点位 ID：“{definition.Id}”。", nameof(definitions));
            if (!physicalChannels.Add(new(definition.DeviceId, definition.Direction, definition.Channel)))
                throw new ArgumentException($"重复的模拟量物理通道：“{definition.DeviceId}/{definition.Direction}/{definition.Channel}”。", nameof(definitions));

            (definition.Direction == AnalogIoDirection.Input ? inputItems : outputItems).Add(definition);
        }

        Definitions = byId.Values.ToArray();
        inputs = inputItems.ToArray();
        outputs = outputItems.ToArray();
    }

    public IReadOnlyCollection<AnalogIoPointDefinition> Definitions { get; }

    public AnalogIoPointDefinition GetRequired(string pointId)
        => TryGet(pointId, out AnalogIoPointDefinition definition)
            ? definition
            : throw new KeyNotFoundException($"未定义的模拟量 IO 点位 ID：“{pointId}”。");

    public bool TryGet(string pointId, out AnalogIoPointDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pointId);
        return byId.TryGetValue(pointId, out definition!);
    }

    public IReadOnlyCollection<AnalogIoPointDefinition> GetByDirection(AnalogIoDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        return direction == AnalogIoDirection.Input ? inputs : outputs;
    }

    private readonly record struct PhysicalChannel(string DeviceId, AnalogIoDirection Direction, int Channel);

    private sealed class PhysicalChannelComparer : IEqualityComparer<PhysicalChannel>
    {
        public static PhysicalChannelComparer Instance { get; } = new();
        public bool Equals(PhysicalChannel x, PhysicalChannel y)
            => x.Direction == y.Direction && x.Channel == y.Channel
                && string.Equals(x.DeviceId, y.DeviceId, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode(PhysicalChannel obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.DeviceId), obj.Direction, obj.Channel);
    }
}
