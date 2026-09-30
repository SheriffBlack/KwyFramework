using Kwy.Communicate.Core;
using Kwy.Communicate.Mqtt;
using MQTTnet;
using MQTTnet.Server;
using System.Net;
using System.Net.Sockets;

namespace Kwy.Communicate.Tests;

public sealed class MqttCommunicationTests
{
    [Fact]
    public void Defaults_AreSecureAndMatchNonTlsPort()
    {
        var config = new MqttConfig();

        Assert.Equal(1883, config.Port);
        Assert.False(config.UseTls);
        Assert.False(config.DangerousAcceptAnyServerCertificate);
    }

    [Fact]
    public void Constructor_TakesConfigurationAndSubscriptionSnapshot()
    {
        var config = CreateValidConfig();
        config.SubscribeTopics = new[] { "factory/status" };
        using var client = new MqttCommunication(config);

        config.Host = "changed.invalid";
        config.SubscribeTopics = new[] { "changed/topic" };

        Assert.Equal(new[] { "factory/status" }, client.GetSubscribedTopics());
    }

    [Fact]
    public void Validate_RejectsInvalidTopicsAndOverflowStrategy()
    {
        MqttConfig config = CreateValidConfig();
        config.SubscribeTopics = new[] { "valid/topic", "" };
        Assert.False(config.Validate());

        config.SubscribeTopics = Array.Empty<string>();
        config.PublishTopic = "invalid/#";
        Assert.False(config.Validate());

        config.PublishTopic = null;
        config.MessageOverflowStrategy = (MqttMessageOverflowStrategy)99;
        Assert.False(config.Validate());
    }

    [Fact]
    public async Task Publish_RejectsInvalidTopicAndQualityOfServiceBeforeConnectionCheck()
    {
        await using var client = new MqttCommunication(CreateValidConfig());

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.PublishAsync(new MqttMessage("invalid/+", ReadOnlyMemory<byte>.Empty)).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.PublishAsync(new MqttMessage("valid/topic", ReadOnlyMemory<byte>.Empty, 3)).AsTask());
    }

    [Fact]
    public async Task Subscribe_RejectsEmptyTopicFilterBeforeConnectionCheck()
    {
        await using var client = new MqttCommunication(CreateValidConfig());

        await Assert.ThrowsAsync<ArgumentException>(() => client.SubscribeAsync(new[] { "" }));
    }

    [Fact]
    public void Factory_CreatesMqttClientWithoutDependencyInjection()
    {
        CommunicationFactory factory = new CommunicationFactoryBuilder()
            .RegisterMqtt()
            .Build();

        using var client = factory.Create<MqttCommunication, MqttConfig>(CreateValidConfig());

        Assert.IsType<MqttCommunication>(client);
    }

    [Fact]
    public async Task Connect_RestoresInitialSubscription_AndReceivesPublishedMessage()
    {
        int port = ReserveTcpPort();
        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
            .WithDefaultEndpointPort(port)
            .Build();
        using MqttServer server = new MqttFactory().CreateMqttServer(serverOptions);
        await server.StartAsync();

        try
        {
            MqttConfig config = CreateValidConfig();
            config.Host = IPAddress.Loopback.ToString();
            config.Port = port;
            config.SubscribeTopics = new[] { "kwy/integration/status" };
            await using var client = new MqttCommunication(config);

            await client.ConnectAsync();
            await client.PublishAsync("kwy/integration/status", "online"u8.ToArray());

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using IAsyncEnumerator<MqttMessage> messages = client
                .ReadMessagesAsync(timeout.Token)
                .GetAsyncEnumerator(timeout.Token);
            Assert.True(await messages.MoveNextAsync());
            MqttMessage received = messages.Current;

            Assert.Equal("kwy/integration/status", received.Topic);
            Assert.Equal("online"u8.ToArray(), received.Payload.ToArray());
        }
        finally
        {
            await server.StopAsync(new MqttServerStopOptionsBuilder().Build());
        }
    }

    private static MqttConfig CreateValidConfig()
        => new()
        {
            ClientId = $"kwy-test-{Guid.NewGuid():N}",
            AutoReconnect = false
        };

    private static int ReserveTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
