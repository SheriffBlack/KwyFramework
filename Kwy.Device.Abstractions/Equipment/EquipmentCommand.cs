namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// 表示发送给整机应用层的设备命令。
/// </summary>
/// <param name="Name">命令名称。</param>
/// <param name="Parameters">命令参数。</param>
/// <param name="RequestedBy">命令请求方，可为空。</param>
public sealed record EquipmentCommand(
    string Name,
    IReadOnlyDictionary<string, string> Parameters,
    string? RequestedBy = null);

/// <summary>
/// 表示设备命令的执行结果。
/// </summary>
/// <param name="Accepted">命令是否被接受。</param>
/// <param name="Message">结果说明，可为空。</param>
public sealed record EquipmentCommandResult(bool Accepted, string? Message = null)
{
    /// <summary>
    /// 创建一个表示命令执行成功的结果。
    /// </summary>
    /// <param name="message">结果说明，可为空。</param>
    /// <returns>命令执行成功的结果。</returns>
    public static EquipmentCommandResult Success(string? message = null) => new(true, message);

    /// <summary>
    /// 创建一个表示命令被拒绝的结果。
    /// </summary>
    /// <param name="message">拒绝原因。</param>
    /// <returns>命令被拒绝的结果。</returns>
    public static EquipmentCommandResult Reject(string message) => new(false, message);
}

/// <summary>
/// 定义设备命令的统一执行入口。
/// </summary>
public interface IEquipmentCommandBus
{
    /// <summary>
    /// 异步执行设备命令。
    /// </summary>
    /// <param name="command">要执行的设备命令。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>设备命令的执行结果。</returns>
    Task<EquipmentCommandResult> ExecuteAsync(
        EquipmentCommand command,
        CancellationToken cancellationToken = default);
}
