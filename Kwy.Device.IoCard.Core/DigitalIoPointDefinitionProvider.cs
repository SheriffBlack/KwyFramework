using Kwy.Device.IoCard.Abstractions;

namespace Kwy.Device.IoCard.Core;

/// <summary>
/// 基于设备配置创建的 IO 点位定义目录。
/// 仅负责稳定 ID 到 <see cref="DigitalIoPointDefinition"/> 的只读查询；设备存在性、通道范围和物理通道重复由
/// <see cref="DigitalIoStateMonitor"/> 在绑定实际硬件时校验。
/// </summary>
public sealed class DigitalIoPointDefinitionProvider : IDigitalIoPointDefinitionProvider
{
    private readonly Dictionary<string, DigitalIoPointDefinition> definitions;
    private readonly IReadOnlyCollection<DigitalIoPointDefinition> inputs;
    private readonly IReadOnlyCollection<DigitalIoPointDefinition> outputs;

    public DigitalIoPointDefinitionProvider(IEnumerable<DigitalIoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        this.definitions = new Dictionary<string, DigitalIoPointDefinition>(StringComparer.OrdinalIgnoreCase);
        var inputItems = new List<DigitalIoPointDefinition>();
        var outputItems = new List<DigitalIoPointDefinition>();

        foreach (DigitalIoPointDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!this.definitions.TryAdd(definition.Id, definition))
                throw new ArgumentException($"重复的 IO 点位 ID：'{definition.Id}'。", nameof(definitions));

            (definition.Direction == DigitalIoDirection.Input ? inputItems : outputItems).Add(definition);
        }

        Definitions = this.definitions.Values.ToArray();
        inputs = inputItems.ToArray();
        outputs = outputItems.ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<DigitalIoPointDefinition> Definitions { get; }

    /// <inheritdoc />
    public DigitalIoPointDefinition GetRequired(string pointId)
        => TryGet(pointId, out DigitalIoPointDefinition definition)
            ? definition
            : throw new KeyNotFoundException($"未定义的 IO 点位 ID：'{pointId}'。");

    /// <inheritdoc />
    public bool TryGet(string pointId, out DigitalIoPointDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pointId);
        return definitions.TryGetValue(pointId, out definition!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<DigitalIoPointDefinition> GetByDirection(DigitalIoDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        return direction == DigitalIoDirection.Input ? inputs : outputs;
    }
}
