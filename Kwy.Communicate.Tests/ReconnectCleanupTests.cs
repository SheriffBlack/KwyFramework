using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.Tests;

public sealed class ReconnectCleanupTests
{
    [Fact]
    public async Task FailedFinalReconnectAttempt_CleansPartialTransport()
    {
        var client = new FailingReconnectClient(new ReconnectConfig());

        await client.StartReconnectAsync();

        // 一次用于重连前清理旧连接，一次用于清理失败尝试产生的部分资源。
        Assert.Equal(2, client.DisconnectCalls);
        await client.DisposeAsync();
    }

    private sealed class ReconnectConfig : IProtocolConfig
    {
        public int Timeout => 1000;
        public bool AutoReconnect => true;
        public int MaxReconnectAttempts => 1;
        public int ReconnectInterval => 0;
        public bool Validate() => true;
    }

    private sealed class FailingReconnectClient(IProtocolConfig config) : CommunicationClientBase(config)
    {
        public int DisconnectCalls { get; private set; }
        public Task StartReconnectAsync() => TriggerReconnectAsync();
        protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
            => throw new IOException("Expected test failure after partial allocation.");
        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            DisconnectCalls++;
            return Task.CompletedTask;
        }
        protected override bool IsConnectionAlive() => false;
    }
}
