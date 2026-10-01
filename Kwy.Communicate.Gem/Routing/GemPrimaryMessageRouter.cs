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

    public async Task<bool> RouteAsync(PrimaryMessageWrapper primary, CancellationToken cancellationToken = default)
    {
        SecsMessage message = primary.PrimaryMessage;
        var id = new SecsMessageId(message.S, message.F);
        if (!handlers.TryGetValue(id, out IGemPrimaryMessageHandler? handler))
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
        await foreach (PrimaryMessageWrapper primary in client.GetPrimaryMessagesAsync(cancellationToken).ConfigureAwait(false))
            await RouteAsync(primary, cancellationToken).ConfigureAwait(false);
    }
}
