using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Core;

namespace Kwy.Communicate.Tests;

public sealed class CommunicationFactoryTests
{
    [Fact]
    public void CreateClient_UsesRegisteredBaseConfigForDerivedConfig()
    {
        CommunicationFactory factory = new CommunicationFactoryBuilder()
            .RegisterCreator<TestConfig>(config => new TestClient(config))
            .Build();

        using ICommunicationClient client = factory.CreateClient(new DerivedTestConfig());

        Assert.IsType<TestClient>(client);
    }

    [Fact]
    public void Builder_RejectsDuplicateConfigurationType()
    {
        var builder = new CommunicationFactoryBuilder()
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

    internal sealed class TestClient(IProtocolConfig config) : CommunicationClientBase(config)
    {
        protected override Task ConnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override bool IsConnectionAlive() => true;
    }
}
