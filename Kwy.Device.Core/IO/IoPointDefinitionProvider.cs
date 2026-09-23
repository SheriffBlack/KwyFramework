using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.Core.IO;

/// <summary>
/// 基于设备配置创建的 IO 点位定义目录。
/// 仅负责稳定 ID 到 <see cref="IoPointDefinition"/> 的只读查询；设备存在性、通道范围和物理通道重复由
/// <see cref="IoStateMonitor"/> 在绑定实际硬件时校验。
/// </summary>
public sealed class IoPointDefinitionProvider : IIoPointDefinitionProvider
{
    private readonly Dictionary<string, IoPointDefinition> definitions;
    private readonly IReadOnlyCollection<IoPointDefinition> inputs;
    private readonly IReadOnlyCollection<IoPointDefinition> outputs;

    public IoPointDefinitionProvider(IEnumerable<IoPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        this.definitions = new Dictionary<string, IoPointDefinition>(StringComparer.OrdinalIgnoreCase);
        var inputItems = new List<IoPointDefinition>();
        var outputItems = new List<IoPointDefinition>();

        foreach (IoPointDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!this.definitions.TryAdd(definition.Id, definition))
                throw new ArgumentException($"重复的 IO 点位 ID：'{definition.Id}'。", nameof(definitions));

            (definition.Kind == IoSignalKind.DigitalInput ? inputItems : outputItems).Add(definition);
        }

        Definitions = this.definitions.Values.ToArray();
        inputs = inputItems.ToArray();
        outputs = outputItems.ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IoPointDefinition> Definitions { get; }

    /// <inheritdoc />
    public IoPointDefinition GetRequired(string pointId)
        => TryGet(pointId, out IoPointDefinition definition)
            ? definition
            : throw new KeyNotFoundException($"未定义的 IO 点位 ID：'{pointId}'。");

    /// <inheritdoc />
    public bool TryGet(string pointId, out IoPointDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pointId);
        return definitions.TryGetValue(pointId, out definition!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IoPointDefinition> GetByKind(IoSignalKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        return kind == IoSignalKind.DigitalInput ? inputs : outputs;
    }
}
