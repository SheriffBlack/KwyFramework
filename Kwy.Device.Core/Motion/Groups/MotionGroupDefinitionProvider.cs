using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>启动时加载的运动组定义提供者；重复 ID 在构造时即失败。</summary>
public sealed class MotionGroupDefinitionProvider : IMotionGroupDefinitionProvider
{
    private readonly IReadOnlyDictionary<string, MotionGroupDefinition> groups;
    public MotionGroupDefinitionProvider(IEnumerable<MotionGroupDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var map = new Dictionary<string, MotionGroupDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (MotionGroupDefinition definition in definitions) { definition.Validate(); if (!map.TryAdd(definition.Id, definition)) throw new InvalidOperationException($"Duplicate motion group ID '{definition.Id}'."); }
        groups = map;
        MotionGroups = map.Values.ToArray();
    }
    public IReadOnlyCollection<MotionGroupDefinition> MotionGroups { get; }
    public MotionGroupDefinition GetMotionGroup(string groupId) => groups.TryGetValue(groupId, out MotionGroupDefinition? group) ? group : throw new KeyNotFoundException($"Motion group '{groupId}' was not found.");
}
