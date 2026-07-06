using KwyTemplate.Vision.Executors;

namespace KwyTemplate.Vision.Registries;

/// <summary>
/// 节点执行器注册表：通过 NodeType 查找其对应的运行时逻辑实现
/// </summary>
public class FlowNodeExecutorRegistry
{
    private readonly Dictionary<string, IFlowNodeExecutor> executors = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IFlowNodeExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        executors[executor.NodeType] = executor;
    }

    public IFlowNodeExecutor? GetExecutor(string nodeType)
    {
        executors.TryGetValue(nodeType, out var executor);
        return executor;
    }
}
