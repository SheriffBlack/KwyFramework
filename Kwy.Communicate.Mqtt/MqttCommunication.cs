using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Communicate.Core;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace Kwy.Communicate.Mqtt;

/// <summary>MQTT 消息客户端。</summary>
public sealed class MqttCommunication : CommunicationClientBase, IMqttCommunication
{
    private readonly MqttConfig mqttConfig;
    private readonly MqttFactory mqttFactory = new();
    private readonly SemaphoreSlim operationSemaphore = new(1, 1);
    private readonly object subscriptionSync = new();
    private readonly HashSet<string> subscribedTopics;
    private readonly Channel<MqttMessage> messages;
    private IMqttClient? mqttClient;
    private long droppedMessageCount;

    /// <inheritdoc />
    public event EventHandler<MessageReceivedEventArgs<MqttMessage>>? MessageReceived;

    /// <inheritdoc />
    public long DroppedMessageCount => Interlocked.Read(ref droppedMessageCount);

    /// <summary>根据已验证的配置快照初始化 MQTT 客户端。</summary>
    public MqttCommunication(MqttConfig config) : this(new ConfigSnapshot(CreateSnapshot(config))) { }

    private MqttCommunication(ConfigSnapshot snapshot) : base(snapshot.Value)
    {
        mqttConfig = snapshot.Value;
        subscribedTopics = new HashSet<string>(mqttConfig.SubscribeTopics, StringComparer.Ordinal);
        messages = Channel.CreateBounded<MqttMessage>(new BoundedChannelOptions(mqttConfig.MessageBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    /// <inheritdoc />
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = mqttFactory.CreateMqttClient();
            client.ApplicationMessageReceivedAsync += OnMqttMessageReceived;
            client.DisconnectedAsync += OnMqttDisconnected;
            mqttClient = client;

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
                        mqttConfig.DangerousAcceptAnyServerCertificate
                        || context.SslPolicyErrors == System.Net.Security.SslPolicyErrors.None);
                });
            }

            if (!string.IsNullOrEmpty(mqttConfig.Username))
                optionsBuilder.WithCredentials(mqttConfig.Username, mqttConfig.Password ?? string.Empty);

            using var timeoutCancellation = new CancellationTokenSource(mqttConfig.Timeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
            try
            {
                MqttClientConnectResult result = await client.ConnectAsync(
                    optionsBuilder.Build(), linkedCancellation.Token).ConfigureAwait(false);
                if (result.ResultCode != MqttClientConnectResultCode.Success)
                    throw new InvalidOperationException($"MQTT 连接失败：{result.ResultCode}。");
            }
            catch (OperationCanceledException ex) when (
                timeoutCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"MQTT 连接在 {mqttConfig.Timeout} 毫秒后超时。", ex);
            }
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task OnConnectedAsync(CancellationToken cancellationToken)
    {
        string[] topics;
        lock (subscriptionSync)
            topics = subscribedTopics.ToArray();
        if (topics.Length == 0)
            return;

        await operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IMqttClient client = mqttClient ?? throw new InvalidOperationException("MQTT 客户端尚未初始化。");
            await SubscribeCoreAsync(client, topics, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IMqttClient? client = mqttClient;
            mqttClient = null;
            if (client == null)
                return;

            client.ApplicationMessageReceivedAsync -= OnMqttMessageReceived;
            client.DisconnectedAsync -= OnMqttDisconnected;
            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                client.Dispose();
            }
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override bool IsConnectionAlive() => mqttClient?.IsConnected == true;

    /// <inheritdoc />
    public async ValueTask PublishAsync(MqttMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        ValidateMessage(message);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        CancellationToken operationToken = linkedCancellation.Token;
        await operationSemaphore.WaitAsync(operationToken).ConfigureAwait(false);
        IMqttClient? client = null;
        try
        {
            client = GetConnectedClient();
            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(message.Topic)
                .WithPayload(message.Payload.ToArray())
                .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)message.QualityOfServiceLevel)
                .WithRetainFlag(message.Retain)
                .Build();
            await client.PublishAsync(applicationMessage, operationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (client != null && !client.IsConnected)
                await HandleCommunicationFailureAsync(ex, $"MQTT 发布失败：{ex.Message}").ConfigureAwait(false);
            throw;
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    /// <summary>向指定主题发布二进制负载。</summary>
    public ValueTask PublishAsync(string topic, ReadOnlyMemory<byte> payload, byte? qualityOfServiceLevel = null,
        bool retain = false, CancellationToken cancellationToken = default)
        => PublishAsync(new MqttMessage(topic, payload,
            qualityOfServiceLevel ?? mqttConfig.QualityOfServiceLevel, retain), cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<MqttMessage> ReadMessagesAsync(CancellationToken cancellationToken = default)
        => messages.Reader.ReadAllAsync(cancellationToken);

    /// <summary>订阅一个主题筛选器。</summary>
    public Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
        => SubscribeAsync(new[] { topic }, cancellationToken);

    /// <inheritdoc />
    public async Task SubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        string[] topicList = MaterializeTopics(topics);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        CancellationToken operationToken = linkedCancellation.Token;
        await operationSemaphore.WaitAsync(operationToken).ConfigureAwait(false);
        IMqttClient? client = null;
        try
        {
            client = GetConnectedClient();
            await SubscribeCoreAsync(client, topicList, operationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (client != null && !client.IsConnected)
                await HandleCommunicationFailureAsync(ex, $"MQTT 订阅失败：{ex.Message}").ConfigureAwait(false);
            throw;
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    /// <summary>取消订阅一个主题筛选器。</summary>
    public Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
        => UnsubscribeAsync(new[] { topic }, cancellationToken);

    /// <inheritdoc />
    public async Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        string[] topicList = MaterializeTopics(topics);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, LifetimeToken);
        CancellationToken operationToken = linkedCancellation.Token;
        await operationSemaphore.WaitAsync(operationToken).ConfigureAwait(false);
        IMqttClient? client = null;
        try
        {
            client = GetConnectedClient();
            var builder = new MqttClientUnsubscribeOptionsBuilder();
            foreach (string topic in topicList)
                builder.WithTopicFilter(topic);

            MqttClientUnsubscribeResult result = await client.UnsubscribeAsync(builder.Build(), operationToken).ConfigureAwait(false);
            string[] failures = result.Items
                .Where(item => item.ResultCode != MqttClientUnsubscribeResultCode.Success
                    && item.ResultCode != MqttClientUnsubscribeResultCode.NoSubscriptionExisted)
                .Select(item => $"{item.TopicFilter}: {item.ResultCode}")
                .ToArray();

            lock (subscriptionSync)
            {
                foreach (MqttClientUnsubscribeResultItem item in result.Items)
                {
                    if (item.ResultCode is MqttClientUnsubscribeResultCode.Success
                        or MqttClientUnsubscribeResultCode.NoSubscriptionExisted)
                        subscribedTopics.Remove(item.TopicFilter);
                }
            }

            if (failures.Length > 0)
                throw new InvalidOperationException($"MQTT 取消订阅被拒绝：{string.Join(", ", failures)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (client != null && !client.IsConnected)
                await HandleCommunicationFailureAsync(ex, $"MQTT 取消订阅失败：{ex.Message}").ConfigureAwait(false);
            throw;
        }
        finally
        {
            operationSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSubscribedTopics()
    {
        lock (subscriptionSync)
            return subscribedTopics.ToArray();
    }

    private async Task SubscribeCoreAsync(IMqttClient client, IReadOnlyCollection<string> topics,
        CancellationToken cancellationToken)
    {
        var builder = new MqttClientSubscribeOptionsBuilder();
        foreach (string topic in topics)
            builder.WithTopicFilter(topic, (MqttQualityOfServiceLevel)mqttConfig.QualityOfServiceLevel);

        MqttClientSubscribeResult result = await client.SubscribeAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
        var failures = new List<string>();
        lock (subscriptionSync)
        {
            foreach (MqttClientSubscribeResultItem item in result.Items)
            {
                if (item.ResultCode is MqttClientSubscribeResultCode.GrantedQoS0
                    or MqttClientSubscribeResultCode.GrantedQoS1
                    or MqttClientSubscribeResultCode.GrantedQoS2)
                    subscribedTopics.Add(item.TopicFilter.Topic);
                else
                    failures.Add($"{item.TopicFilter.Topic}: {item.ResultCode}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException($"MQTT 订阅被拒绝：{string.Join(", ", failures)}");
    }

    private async Task OnMqttMessageReceived(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        MqttApplicationMessage? applicationMessage = eventArgs.ApplicationMessage;
        if (applicationMessage == null || disposed)
            return;

        var message = new MqttMessage(applicationMessage.Topic, applicationMessage.PayloadSegment.ToArray(),
            (byte)applicationMessage.QualityOfServiceLevel, applicationMessage.Retain);
        try
        {
            await BufferMessageAsync(message).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (LifetimeToken.IsCancellationRequested || disposed)
        {
            return;
        }
        EnqueueObserverNotification(() => MessageReceived?.Invoke(this, new MessageReceivedEventArgs<MqttMessage>(message)));
    }

    private async ValueTask BufferMessageAsync(MqttMessage message)
    {
        switch (mqttConfig.MessageOverflowStrategy)
        {
            case MqttMessageOverflowStrategy.Wait:
                await messages.Writer.WriteAsync(message, LifetimeToken).ConfigureAwait(false);
                break;
            case MqttMessageOverflowStrategy.DropNewest:
                if (!messages.Writer.TryWrite(message))
                    Interlocked.Increment(ref droppedMessageCount);
                break;
            case MqttMessageOverflowStrategy.DropOldest:
                while (!messages.Writer.TryWrite(message))
                {
                    if (messages.Reader.TryRead(out _))
                        Interlocked.Increment(ref droppedMessageCount);
                    else
                        await Task.Yield();
                }
                break;
            default:
                throw new InvalidOperationException($"不支持的 MQTT 消息溢出策略：{mqttConfig.MessageOverflowStrategy}。");
        }
    }

    private async Task OnMqttDisconnected(MqttClientDisconnectedEventArgs eventArgs)
    {
        if (State is ConnectionState.Disconnected or ConnectionState.Disconnecting || disposed)
            return;
        Exception exception = eventArgs.Exception ?? new IOException(eventArgs.ReasonString ?? "MQTT 连接已关闭。");
        await HandleCommunicationFailureAsync(exception, eventArgs.ReasonString ?? exception.Message).ConfigureAwait(false);
    }

    private IMqttClient GetConnectedClient()
    {
        IMqttClient? client = mqttClient;
        if (State != ConnectionState.Connected || client?.IsConnected != true)
            throw new InvalidOperationException("MQTT 客户端尚未连接。");
        return client;
    }

    private static string[] MaterializeTopics(IEnumerable<string> topics)
    {
        ArgumentNullException.ThrowIfNull(topics);
        string[] topicList = topics.Distinct(StringComparer.Ordinal).ToArray();
        if (topicList.Length == 0)
            throw new ArgumentException("至少需要提供一个 MQTT 主题筛选器。", nameof(topics));
        if (topicList.Any(topic => !MqttConfig.IsValidTopicFilter(topic)))
            throw new ArgumentException("MQTT 主题筛选器不能为空，也不能包含空字符。", nameof(topics));
        return topicList;
    }

    private static void ValidateMessage(MqttMessage message)
    {
        if (!MqttConfig.IsValidPublishTopic(message.Topic))
            throw new ArgumentException("MQTT 发布主题不能为空、不能包含通配符，也不能包含空字符。", nameof(message));
        if (message.QualityOfServiceLevel > 2)
            throw new ArgumentOutOfRangeException(nameof(message), "MQTT 服务质量等级必须介于 0 和 2 之间。");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        await base.DisposeAsync().ConfigureAwait(false);
        messages.Writer.TryComplete();
        if (operationSemaphore.Wait(0))
            operationSemaphore.Dispose();
    }

    private static MqttConfig CreateSnapshot(MqttConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Validate())
            throw new ArgumentException("MQTT 配置无效。", nameof(config));
        return config.Snapshot();
    }

    private sealed record ConfigSnapshot(MqttConfig Value);
}
