using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.Tests;

public sealed class ProtocolClientFactoryTests
{
    [Fact]
    public void CreateClient_UsesRegisteredBaseConfigForDerivedConfig()
    {
        ProtocolClientFactory factory = new ProtocolClientFactoryBuilder()
            .RegisterCreator<TestConfig>(config => new TestClient(config))
            .Build();

        using IProtocolClient client = factory.CreateClient(new DerivedTestConfig());

        Assert.IsType<TestClient>(client);
    }

    [Fact]
    public void Builder_RejectsDuplicateConfigurationType()
    {
        var builder = new ProtocolClientFactoryBuilder()
            .RegisterCreator<TestConfig>(config => new TestClient(config));

        Assert.Throws<InvalidOperationException>(() =>
            builder.RegisterCreator<TestConfig>(config => new TestClient(config)));
    }

    internal class TestConfig : IProtocolConfig
    {
        public int Timeout { get; set; } = 1000;
        public bool AutoReconnect { get; set; }
        public int MaxReconnectAttempts { get; set; } = 1;
        public int ReconnectInterval { get; set; }
        public virtual bool Validate() => true;
    }

    private sealed class DerivedTestConfig : TestConfig;

    internal sealed class TestClient(IProtocolConfig config) : ProtocolClientBase(config)
    {
        protected override Task ConnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override bool IsConnectionAlive() => true;
    }
}
