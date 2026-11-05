using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>按 S/F 分发 Host Primary Message，并保证每条消息最多回复一次。</summary>
public sealed class GemPrimaryMessageRouter
{
    private readonly Dictionary<SecsMessageId, IGemPrimaryMessageHandler> handlers = new();

    public GemPrimaryMessageRouter Register(IGemPrimaryMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!handlers.TryAdd(handler.MessageId, handler))
            throw new InvalidOperationException($"A handler for {handler.MessageId} is already registered.");
        return this;
    }

    /// <summary>
    /// 消费并路由一条入站 Primary Message。方法返回前始终释放
    /// <see cref="PrimaryMessageWrapper.PrimaryMessage"/>，Handler 不得在返回后继续持有其 Item。
    /// </summary>
    public async Task<bool> RouteAsync(PrimaryMessageWrapper primary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(primary);
        using SecsMessage message = primary.PrimaryMessage;
        var id = new SecsMessageId(message.S, message.F);
        if (!handlers.TryGetValue(id, out IGemPrimaryMessageHandler? handler))  // 如果没有注册对应的 Handler，则直接回复 S9F7（底层Secs4net 实现）
        {
            if (message.ReplyExpected)
                await primary.TryReplyAsync(null, cancellationToken).ConfigureAwait(false);
            return false;
        }

        SecsMessage? reply = await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
        if (message.ReplyExpected)
        {
            try
            {
                await primary.TryReplyAsync(reply, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                reply?.Dispose();
            }
        }
        else
        {
            reply?.Dispose();
        }
        return true;
    }

    public async Task RunAsync(ISecsGemClient client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await foreach (PrimaryMessageWrapper primary in client.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            await RouteAsync(primary, cancellationToken).ConfigureAwait(false);
    }
}
