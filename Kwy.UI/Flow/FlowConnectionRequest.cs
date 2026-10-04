namespace Kwy.UI.Flow;

/// <summary>
/// 请求建立流程连线时所需的通用端口信息。
/// </summary>
public sealed record FlowConnectionRequest(
    object? SourceConnector,
    object? SourceNode,
    object? TargetConnector,
    object? TargetNode);

/// <summary>
/// 应用层提供的流程连线合法性校验契约。
/// </summary>
public interface IFlowConnectionValidator
{
    bool CanConnect(FlowConnectionRequest request);
}
