using Kwy.Device.Abstractions.PLC;

namespace Kwy.Device.Core.PLC;

/// <summary>
/// PLC 点位定义的只读查询实现。
/// 在构造阶段完成点位定义、业务 ID 和物理地址唯一性校验；实际 PLC 设备是否已连接由业务读写服务在调用时确认。
/// </summary>
public sealed class PlcPointDefinitionProvider : IPlcPointDefinitionProvider
{
    private readonly IReadOnlyDictionary<string, PlcPointDefinition> byId;

    /// <summary>使用一组静态点位定义创建目录。</summary>
    public PlcPointDefinitionProvider(IEnumerable<PlcPointDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        PlcPointDefinition[] items = definitions.ToArray();
        var result = new Dictionary<string, PlcPointDefinition>(StringComparer.OrdinalIgnoreCase);
        var physicalAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (PlcPointDefinition point in items)
        {
            ArgumentNullException.ThrowIfNull(point);
            point.Validate();
            if (!result.TryAdd(point.Id, point))
                throw new InvalidOperationException($"PLC 点位 ID 重复：{point.Id}。");

            string physicalAddress = $"{point.DeviceId}\u001f{point.Address}";
            if (!physicalAddresses.Add(physicalAddress))
                throw new InvalidOperationException($"PLC 物理地址重复：{point.DeviceId}/{point.Address}。");
        }

        byId = result;
        Definitions = items;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<PlcPointDefinition> Definitions { get; }

    /// <inheritdoc/>
    public bool TryGet(string pointId, out PlcPointDefinition point)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pointId);
        return byId.TryGetValue(pointId, out point!);
    }

    /// <inheritdoc/>
    public PlcPointDefinition GetRequired(string pointId)
        => TryGet(pointId, out PlcPointDefinition point)
            ? point
            : throw new KeyNotFoundException($"未找到 PLC 点位：{pointId}。");
}
