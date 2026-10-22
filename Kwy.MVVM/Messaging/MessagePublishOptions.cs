namespace Kwy.MVVM.Messaging;

/// <summary>
/// 发布消息时使用的选项。
/// </summary>
public sealed class MessagePublishOptions
{
    public static MessagePublishOptions Default { get; } = new();

    public static MessagePublishOptions Retained { get; } = new() { RetainLatest = true };

    /// <summary>
    /// 将消息保存为最新值，供后续回放订阅使用。
    /// </summary>
    public bool RetainLatest { get; init; }
}
