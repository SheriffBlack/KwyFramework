namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Active-read byte stream transport, such as TCP, serial port, or GPIB.
/// </summary>
public interface IByteTransport : ICommunicationClient
{
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
