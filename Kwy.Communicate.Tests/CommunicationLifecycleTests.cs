using Kwy.Communicate.Abstractions.Enums;
using static Kwy.Communicate.Tests.CommunicationFactoryTests;

namespace Kwy.Communicate.Tests;

public sealed class CommunicationLifecycleTests
{
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
}
