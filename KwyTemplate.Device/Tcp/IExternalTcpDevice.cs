using Kwy.Communicate.Abstractions;
using Kwy.Device.Abstractions;

namespace KwyTemplate.Device.Tcp;

public interface IExternalTcpDevice : IDevice
{
    IByteTransport Transport { get; }

    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
