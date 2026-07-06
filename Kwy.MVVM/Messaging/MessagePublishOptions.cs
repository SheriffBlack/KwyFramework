namespace Kwy.MVVM.Messaging;

/// <summary>
/// Options used when publishing a message.
/// </summary>
public sealed class MessagePublishOptions
{
    public static MessagePublishOptions Default { get; } = new();

    public static MessagePublishOptions Retained { get; } = new() { RetainLatest = true };

    /// <summary>
    /// Stores the message as the latest value for future replay subscriptions.
    /// </summary>
    public bool RetainLatest { get; init; }
}
