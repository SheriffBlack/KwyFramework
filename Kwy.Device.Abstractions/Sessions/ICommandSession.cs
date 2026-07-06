namespace Kwy.Device.Abstractions.Sessions;

public interface ICommandSession
{
    Task<CommandResponse> SendAsync(CommandRequest request, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
