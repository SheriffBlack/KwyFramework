namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 支持非抛异常连接尝试的通信客户端。
/// 适用于设备宿主启动等允许部分硬件离线的场景。
/// </summary>
public interface ITryConnectCommunicationClient
{
    /// <summary>
    /// 尝试建立连接。成功返回 <see langword="true"/>；
    /// 可预期的连接失败返回 <see langword="false"/>，并通过状态和错误事件报告。
    /// 取消操作仍会抛出 <see cref="OperationCanceledException"/>。
    /// </summary>
    Task<bool> TryConnectAsync(CancellationToken cancellationToken = default);
}
