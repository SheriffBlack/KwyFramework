using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 设备级业务轴定义目录。
/// 在构造时聚合已加载的轴定义并检查业务 ID、物理卡与通道的唯一性；控制器连通性与通道能力由启动配置校验器负责确认。
/// </summary>
public sealed class AxisDefinitionProvider : IAxisDefinitionProvider
{
    private readonly Dictionary<string, AxisDefinition> byId;
    private readonly Dictionary<AxisAddress, AxisDefinition> byAddress;

    public AxisDefinitionProvider(IEnumerable<AxisDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        byId = new Dictionary<string, AxisDefinition>(StringComparer.OrdinalIgnoreCase);
        byAddress = new Dictionary<AxisAddress, AxisDefinition>();

        foreach (AxisDefinition definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            definition.Validate();
            if (!byId.TryAdd(definition.Id, definition))
                throw new ArgumentException($"重复的业务轴 ID：'{definition.Id}'。", nameof(definitions));
            if (!byAddress.TryAdd(definition.PhysicalId, definition))
                throw new ArgumentException($"重复的物理轴地址：'{definition.PhysicalId}'。", nameof(definitions));
        }

        Definitions = byId.Values.ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AxisDefinition> Definitions { get; }

    /// <inheritdoc />
    public AxisDefinition GetRequired(string axisId)
        => TryGet(axisId, out AxisDefinition definition)
            ? definition
            : throw new KeyNotFoundException($"未定义的业务轴 ID：'{axisId}'。");

    /// <inheritdoc />
    public bool TryGet(string axisId, out AxisDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(axisId);
        return byId.TryGetValue(axisId, out definition!);
    }

    /// <inheritdoc />
    public bool TryGet(AxisAddress address, out AxisDefinition definition)
        => byAddress.TryGetValue(address, out definition!);
}
