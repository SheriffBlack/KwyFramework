namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Text command/query capability used by instruments and GPIB devices.
/// </summary>
public interface ICommandQueryClient : ICommunicationClient
{
    ValueTask WriteCommandAsync(string command, CancellationToken cancellationToken = default);
    ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default);
}
