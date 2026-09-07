using Kwy.Communicate.Abstractions;

namespace Kwy.Communicate.Secs;

public interface ISecsClient : ICommunicationClient
{
    HsmsSessionState SessionState { get; }

    event EventHandler<SecsMessageReceivedEventArgs>? PrimaryMessageReceived;

    Task SendAsync(SecsMessage message, CancellationToken cancellationToken = default);

    Task<SecsMessage> SendPrimaryAsync(SecsMessage message, CancellationToken cancellationToken = default);
}
