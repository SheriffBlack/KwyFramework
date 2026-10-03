using Kwy.Communicate.Abstractions.Enums;
using static Kwy.Communicate.Tests.CommunicationFactoryTests;

namespace Kwy.Communicate.Tests;

public sealed class CommunicationLifecycleTests
{
    [Fact]
    public async Task TryConnectAsync_ExpectedConnectionFailure_IsReportedAsErrorState()
    {
        await using var client = new FailingClient(new TestConfig());
        Exception? reported = null;
        client.ErrorOccurred += (_, args) => reported = args.Exception;

        Exception? thrown = await Record.ExceptionAsync(() => client.TryConnectAsync());

        Assert.Null(thrown);
        Assert.False(client.IsConnected);
        Assert.Equal(ConnectionState.Error, client.State);
        await WaitUntilAsync(() => reported != null);
        Assert.IsType<IOException>(reported);
    }

    [Fact]
    public async Task ThrowingStateObserver_DoesNotBreakConnectionLifecycle()
    {
        await using var client = new TestClient(new TestConfig());
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ConnectionStateChanged += (_, args) =>
        {
            if (args.CurrentState == ConnectionState.Connected)
                connected.TrySetResult();
            throw new InvalidOperationException("Observer failure");
        };

        await client.ConnectAsync();
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(client.IsConnected);
    }

    private sealed class FailingClient(TestConfig config) : Kwy.Communicate.Core.CommunicationClientBase(config)
    {
        protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
            => throw new IOException("设备离线");

        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override bool IsConnectionAlive() => false;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }
}
