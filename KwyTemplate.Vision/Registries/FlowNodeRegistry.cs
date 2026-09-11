using KwyTemplate.Vision.Models;
using KwyTemplate.Vision.NodeDescriptors;

namespace KwyTemplate.Vision.Registries;

/// <summary>
/// 节点注册表：统一管理所有已注册的节点描述符。
/// 在 Module 中注册为单例，FlowEditorViewModel 通过构造注入获取。
/// </summary>
public class FlowNodeRegistry
{
    private readonly Dictionary<string, IFlowNodeDescriptor> descriptors = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IFlowNodeDescriptor> All => descriptors.Values.ToList();

    public void Register(IFlowNodeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptors[descriptor.NodeType] = descriptor;
    }

    public FlowNode CreateNode(string nodeType)
    {
        if (!descriptors.TryGetValue(nodeType, out var desc))
        {
            throw new InvalidOperationException($"未知节点类型: {nodeType}");
        }

        return desc.CreateDefaultNode();
    }
}
