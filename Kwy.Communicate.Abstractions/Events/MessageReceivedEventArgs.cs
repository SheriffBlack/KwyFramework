namespace Kwy.Communicate.Abstractions.Events;

public sealed class MessageReceivedEventArgs<TMessage> : EventArgs
{
    public TMessage Message { get; }

    public MessageReceivedEventArgs(TMessage message)
    {
        Message = message;
    }
}
