using KwyTemplate.Vision.Models;

namespace KwyTemplate.Vision.Executors;

/// <summary>
/// 节点执行器：负责运行时执行一种节点的业务逻辑
/// 与 IFlowNodeDescriptor.NodeType 通过字符串匹配
/// </summary>
public interface IFlowNodeExecutor
{
    /// <summary>对应节点类型</summary>
    string NodeType { get; }

    /// <summary>
    /// 执行节点。
    /// </summary>
    /// <param name="node">当前节点数据（含参数）</param>
    /// <param name="context">本次流程运行上下文。</param>
    /// <param name="inputs">上游输出数据。Key = 端口名, Value = 端口值。</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>当前节点执行结果。</returns>
    Task<FlowNodeExecutionResult> ExecuteAsync(
        FlowNode node,
        FlowExecutionContext context,
        IReadOnlyDictionary<string, FlowValue> inputs,
        CancellationToken ct = default);
}
