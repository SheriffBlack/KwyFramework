namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 面向主动读写字节流的通信客户端，例如 TCP、串口或 VISA/GPIB。
/// </summary>
public interface IByteStreamClient : IProtocolClient
{
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
