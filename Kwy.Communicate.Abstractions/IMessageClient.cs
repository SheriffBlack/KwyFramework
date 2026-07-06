namespace Kwy.Communicate.Abstractions;

using Kwy.Communicate.Abstractions.Events;

/// <summary>
/// Message-oriented communication client.
/// </summary>
public interface IMessageClient<TMessage> : ICommunicationClient
{
    event EventHandler<MessageReceivedEventArgs<TMessage>>? MessageReceived;
    ValueTask PublishAsync(TMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TMessage> ReadMessagesAsync(CancellationToken cancellationToken = default);
}
