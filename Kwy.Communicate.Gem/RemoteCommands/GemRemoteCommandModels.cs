using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>S2F42 的 HCACK 远程命令应答码。</summary>
public enum GemHostCommandAckCode : byte
{
    /// <summary>命令已执行完成。</summary>
    Completed = 0,
    /// <summary>命令不存在。</summary>
    CommandNotFound = 1,
    /// <summary>当前无法执行。</summary>
    CannotPerformNow = 2,
    /// <summary>命令参数缺失、格式错误或越界。</summary>
    InvalidParameter = 3,
    /// <summary>命令已受理，完成结果稍后通过事件上报。</summary>
    Accepted = 4,
    /// <summary>设备已处于目标状态。</summary>
    AlreadyInDesiredCondition = 5,
    /// <summary>命令引用的对象不存在。</summary>
    ObjectNotFound = 6
}

/// <summary>远程命令最终执行结果 </summary>
/// <remarks>和 AckCode 形成两级应答，该状态表示异步执行结果（通常通过 S6F11 事件上报）。</remarks>
public enum GemRemoteCommandCompletionStatus
{
    /// <summary>尚未给出执行结果。</summary>
    Unspecified,
    /// <summary>已接收，等待业务执行完成。</summary>
    Accepted,
    /// <summary>被拒绝 </summary>
    Rejected,
    /// <summary>正常执行完成。</summary>
    Completed,
    /// <summary>执行过程失败。</summary>
    Failed,
    /// <summary>被取消（Host下发取消、本地人工停止） </summary>
    Cancelled
}

public sealed record GemRemoteCommandParameterDefinition(string Name, SecsFormat Format, bool Required = true);

public sealed record GemRemoteCommandDefinition(
    string Name,
    IReadOnlyList<GemRemoteCommandParameterDefinition> Parameters,
    IReadOnlySet<GemControlState>? AllowedControlStates = null,
    bool AllowAdditionalParameters = false,
    GemCeid? AcceptedEvent = null,
    GemCeid? CompletedEvent = null,
    GemCeid? FailedEvent = null,
    string? HandlerKey = null)
{
    public string EffectiveHandlerKey => string.IsNullOrWhiteSpace(HandlerKey) ? Name : HandlerKey;
}

public sealed record GemRemoteCommand(string CommandName, IReadOnlyDictionary<string, Item> Parameters)
{
    public bool TryGetParameter(string name, out Item value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach ((string key, Item item) in Parameters)
        {
            if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                continue;
            value = item;
            return true;
        }
        value = null!;
        return false;
    }

    public Item GetRequiredParameter(string name)
        => TryGetParameter(name, out Item? value)
            ? value
            : throw new KeyNotFoundException($"Remote command parameter '{name}' is missing.");
}

public sealed record GemRemoteCommandResult(
    GemHostCommandAckCode AckCode,
    string? Message = null,
    GemRemoteCommandCompletionStatus CompletionStatus = GemRemoteCommandCompletionStatus.Unspecified)
{
    public bool IsAccepted => AckCode is GemHostCommandAckCode.Completed or GemHostCommandAckCode.Accepted;
    public bool IsCompleted => CompletionStatus == GemRemoteCommandCompletionStatus.Completed;

    public GemRemoteCommandResult WithInferredCompletionStatus()
        => CompletionStatus != GemRemoteCommandCompletionStatus.Unspecified
            ? this
            : this with
            {
                CompletionStatus = AckCode switch
                {
                    GemHostCommandAckCode.Completed => GemRemoteCommandCompletionStatus.Completed,
                    GemHostCommandAckCode.Accepted => GemRemoteCommandCompletionStatus.Accepted,
                    _ => GemRemoteCommandCompletionStatus.Rejected
                }
            };
}
