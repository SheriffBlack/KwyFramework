using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using System.Text;

namespace Kwy.Communicate.Tests;

public sealed class ByteStreamClientExtensionsTests
{
    [Fact]
    public async Task ReadExactlyAsync_CombinesPartialReads()
    {
        await using var client = new StubByteStreamClient([new byte[] { 1, 2 }, new byte[] { 3 }, new byte[] { 4, 5 }]);
        var buffer = new byte[5];

        await client.ReadExactlyAsync(buffer);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Fact]
    public async Task ReadExactlyAsync_ZeroLengthReadBeforeCompletion_ThrowsEndOfStream()
    {
        await using var client = new StubByteStreamClient([new byte[] { 1 }, []]);
        var buffer = new byte[2];

        await Assert.ThrowsAsync<EndOfStreamException>(async () => await client.ReadExactlyAsync(buffer));
    }

    [Fact]
    public async Task WriteTextAsync_UsesSpecifiedEncoding()
    {
        await using var client = new StubByteStreamClient([]);

        await client.WriteTextAsync("测试", Encoding.Unicode);

        Assert.Equal(Encoding.Unicode.GetBytes("测试"), client.WrittenBytes);
    }

    private sealed class StubByteStreamClient(IEnumerable<byte[]> readChunks) : IByteStreamClient
    {
        private readonly Queue<byte[]> reads = new(readChunks);

        public byte[] WrittenBytes { get; private set; } = [];
        public ConnectionState State => ConnectionState.Connected;
        public bool IsConnected => true;
#pragma warning disable CS0067
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
#pragma warning restore CS0067

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            WrittenBytes = data.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!reads.TryDequeue(out byte[]? data))
                return ValueTask.FromResult(0);
            data.CopyTo(buffer);
            return ValueTask.FromResult(data.Length);
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
