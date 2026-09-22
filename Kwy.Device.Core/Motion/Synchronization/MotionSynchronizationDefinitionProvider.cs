using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>将设备配置冻结为可按稳定 ID 查询的虚拟轴与同步关系。</summary>
public sealed class MotionSynchronizationDefinitionProvider : IVirtualAxisDefinitionProvider, IMotionSynchronizationDefinitionProvider
{
    private readonly IReadOnlyDictionary<string, VirtualAxisDefinition> virtualAxes;
    private readonly IReadOnlyDictionary<string, ElectronicGearDefinition> gears;
    private readonly IReadOnlyDictionary<string, ElectronicCamDefinition> cams;

    public MotionSynchronizationDefinitionProvider(
        IEnumerable<VirtualAxisDefinition> virtualAxes,
        IEnumerable<ElectronicGearDefinition> gears,
        IEnumerable<ElectronicCamDefinition> cams)
    {
        this.virtualAxes = CreateMap(virtualAxes, item => item.Id, item => item.Validate(), "virtual axis");
        this.gears = CreateMap(gears, item => item.Id, item => item.Validate(), "electronic gear");
        this.cams = CreateMap(cams, item => item.Id, item => item.Validate(), "electronic cam");
        VirtualAxes = this.virtualAxes.Values.ToArray();
        ElectronicGears = this.gears.Values.ToArray();
        ElectronicCams = this.cams.Values.ToArray();
    }

    public IReadOnlyCollection<VirtualAxisDefinition> VirtualAxes { get; }
    public IReadOnlyCollection<ElectronicGearDefinition> ElectronicGears { get; }
    public IReadOnlyCollection<ElectronicCamDefinition> ElectronicCams { get; }

    public VirtualAxisDefinition GetVirtualAxis(string axisId) => Get(virtualAxes, axisId, "Virtual axis");
    public ElectronicGearDefinition GetElectronicGear(string id) => Get(gears, id, "Electronic gear");
    public ElectronicCamDefinition GetElectronicCam(string id) => Get(cams, id, "Electronic cam");

    private static IReadOnlyDictionary<string, T> CreateMap<T>(IEnumerable<T> items, Func<T, string> id, Action<T> validate, string label)
    {
        ArgumentNullException.ThrowIfNull(items);
        var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (T item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            validate(item);
            if (!map.TryAdd(id(item), item)) throw new InvalidOperationException($"Duplicate {label} ID '{id(item)}'.");
        }
        return map;
    }

    private static T Get<T>(IReadOnlyDictionary<string, T> definitions, string id, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return definitions.TryGetValue(id, out T? definition)
            ? definition
            : throw new KeyNotFoundException($"{label} '{id}' was not found.");
    }
}
