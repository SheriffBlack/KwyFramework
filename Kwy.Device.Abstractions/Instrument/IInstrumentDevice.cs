namespace Kwy.Device.Abstractions.Instrument;

public interface ICommandInstrument
{
    ValueTask WriteCommandAsync(string command, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}

public interface IQueryInstrument
{
    ValueTask<string> QueryAsync(string command, CancellationToken cancellationToken = default);
    ValueTask<string> ReadResponseAsync(CancellationToken cancellationToken = default);
}

public interface ITriggeredInstrument
{
    ValueTask TriggerAsync(string command = "*TRG", CancellationToken cancellationToken = default);

    Task<string> WaitAndReadTriggeredResultAsync(
        Func<CancellationToken, Task> waitForCompletionAsync,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Instrument device with serialized command, query, trigger, and result-read operations.
/// </summary>
public interface IInstrumentDevice :
    IDevice,
    IConfigurableDevice,
    ICommandInstrument,
    IQueryInstrument,
    ITriggeredInstrument
{
}
