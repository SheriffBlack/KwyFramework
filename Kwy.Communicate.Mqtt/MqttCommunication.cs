using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Communicate.Core;
using MQTTnet;
using MQTTnet.Client;
using System.Threading.Channels;

namespace Kwy.Communicate.Mqtt;

/// <summary>
/// MQTT message client.
/// </summary>
public sealed class MqttCommunication : CommunicationClientBase, IMqttCommunication
{
    private readonly MqttConfig mqttConfig;
    private readonly MqttFactory mqttFactory = new();
    private readonly SemaphoreSlim subscriptionSemaphore = new(1, 1);
    private readonly Channel<MqttMessage> messages;
    private IMqttClient? mqttClient;

    public event EventHandler<MessageReceivedEventArgs<MqttMessage>>? MessageReceived;

    public MqttCommunication(MqttConfig config) : base(config)
    {
        mqttConfig = config ?? throw new ArgumentNullException(nameof(config));
        var channelOptions = new BoundedChannelOptions(mqttConfig.MessageBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // 丢弃旧数据，保证实时性
            SingleReader = true, // 契合 ReadMessagesAsync 的单消费者设计
            SingleWriter = true
        };

        messages = Channel.CreateBounded<MqttMessage>(channelOptions);
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        mqttClient = mqttFactory.CreateMqttClient();
        mqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceived;
        mqttClient.DisconnectedAsync += OnMqttDisconnected;

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttConfig.Host, mqttConfig.Port)
            .WithClientId(mqttConfig.ClientId)
            .WithCleanSession(mqttConfig.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(mqttConfig.KeepAlivePeriod));

        if (mqttConfig.UseTls)
        {
            optionsBuilder.WithTlsOptions(options =>
            {
                options.UseTls(true);
                options.WithCertificateValidationHandler(context =>
                    mqttConfig.AutoAcceptUntrustedCertificates ||
                    context.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None);
            });
        }

        if (!string.IsNullOrEmpty(mqttConfig.Username))
            optionsBuilder.WithCredentials(mqttConfig.Username, mqttConfig.Password ?? string.Empty);

        var result = await mqttClient.ConnectAsync(optionsBuilder.Build(), cancellationToken);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
            throw new InvalidOperationException($"MQTT connection failed: {result.ResultCode}");
    }

    protected override Task OnConnectedAsync(CancellationToken cancellationToken)
        => mqttConfig.SubscribeTopics.Count == 0
            ? Task.CompletedTask
            : SubscribeAsync(mqttConfig.SubscribeTopics.ToArray(), cancellationToken);

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (mqttClient == null)
            return;

        var client = mqttClient;
        mqttClient = null;
        client.ApplicationMessageReceivedAsync -= OnMqttMessageReceived;
        client.DisconnectedAsync -= OnMqttDisconnected;

        try
        {
            if (client.IsConnected)
                await client.DisconnectAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            client.Dispose();
        }
    }

    protected override bool IsConnectionAlive() => mqttClient?.IsConnected == true;

    public async ValueTask PublishAsync(MqttMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        if (!IsConnected || mqttClient == null)
            throw new InvalidOperationException("MQTT client is not connected.");
        if (string.IsNullOrWhiteSpace(message.Topic))
            throw new ArgumentException("MQTT topic cannot be empty.", nameof(message));

        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic(message.Topic)
            .WithPayload(message.Payload.ToArray())
            .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)message.QualityOfServiceLevel)
            .WithRetainFlag(message.Retain)
            .Build();

        try
        {
            await mqttClient.PublishAsync(applicationMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"MQTT publish failed: {ex.Message}");
            throw;
        }
    }

    public ValueTask PublishAsync(
        string topic,
        ReadOnlyMemory<byte> payload,
        byte? qualityOfServiceLevel = null,
        bool retain = false,
        CancellationToken cancellationToken = default)
        => PublishAsync(new MqttMessage(topic, payload, qualityOfServiceLevel ?? mqttConfig.QualityOfServiceLevel, retain), cancellationToken);

    public IAsyncEnumerable<MqttMessage> ReadMessagesAsync(CancellationToken cancellationToken = default)
        => messages.Reader.ReadAllAsync(cancellationToken);

    public Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
        => SubscribeAsync(new[] { topic }, cancellationToken);

    public async Task SubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(topics);
        if (!IsConnected || mqttClient == null)
            throw new InvalidOperationException("MQTT client is not connected.");

        var topicList = topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct().ToArray();
        if (topicList.Length == 0)
            return;

        await subscriptionSemaphore.WaitAsync(cancellationToken);
        try
        {
            var builder = new MqttClientSubscribeOptionsBuilder();
            foreach (var topic in topicList)
                builder.WithTopicFilter(topic, (MQTTnet.Protocol.MqttQualityOfServiceLevel)mqttConfig.QualityOfServiceLevel);

            var result = await mqttClient.SubscribeAsync(builder.Build(), cancellationToken);
            var items = result.Items.ToArray();
            lock (mqttConfig.SubscribeTopics)
            {
                for (var index = 0; index < items.Length && index < topicList.Length; index++)
                {
                    if ((items[index].ResultCode is MqttClientSubscribeResultCode.GrantedQoS0
                        or MqttClientSubscribeResultCode.GrantedQoS1
                        or MqttClientSubscribeResultCode.GrantedQoS2)
                        && !mqttConfig.SubscribeTopics.Contains(topicList[index]))
                    {
                        mqttConfig.SubscribeTopics.Add(topicList[index]);
                    }
                }
            }
        }
        finally
        {
            subscriptionSemaphore.Release();
        }
    }

    public Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
        => UnsubscribeAsync(new[] { topic }, cancellationToken);

    public async Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(topics);
        if (!IsConnected || mqttClient == null)
            throw new InvalidOperationException("MQTT client is not connected.");

        var topicList = topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct().ToArray();
        if (topicList.Length == 0)
            return;

        await subscriptionSemaphore.WaitAsync(cancellationToken);
        try
        {
            var builder = new MqttClientUnsubscribeOptionsBuilder();
            foreach (var topic in topicList)
                builder.WithTopicFilter(topic);

            await mqttClient.UnsubscribeAsync(builder.Build(), cancellationToken);
            lock (mqttConfig.SubscribeTopics)
            {
                foreach (var topic in topicList)
                    mqttConfig.SubscribeTopics.Remove(topic);
            }
        }
        finally
        {
            subscriptionSemaphore.Release();
        }
    }

    public IReadOnlyList<string> GetSubscribedTopics()
    {
        lock (mqttConfig.SubscribeTopics)
            return mqttConfig.SubscribeTopics.ToArray();
    }

    private Task OnMqttMessageReceived(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        var applicationMessage = eventArgs.ApplicationMessage;
        if (applicationMessage == null)
            return Task.CompletedTask;

        var payload = applicationMessage.PayloadSegment.ToArray();
        var message = new MqttMessage(
            applicationMessage.Topic,
            payload,
            (byte)applicationMessage.QualityOfServiceLevel,
            applicationMessage.Retain);

        messages.Writer.TryWrite(message);
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs<MqttMessage>(message));
        return Task.CompletedTask;
    }

    private async Task OnMqttDisconnected(MqttClientDisconnectedEventArgs eventArgs)
    {
        if (State is ConnectionState.Disconnected or ConnectionState.Disconnecting)
            return;

        await HandleCommunicationFailureAsync(
            new IOException(eventArgs.ReasonString ?? "MQTT connection closed."),
            eventArgs.ReasonString ?? "MQTT connection closed.");
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        await base.DisposeAsync();
        messages.Writer.TryComplete();
        subscriptionSemaphore.Dispose();
    }
}
