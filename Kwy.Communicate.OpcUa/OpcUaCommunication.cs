using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Communicate.Core;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Text;
using System.Threading.Channels;

namespace Kwy.Communicate.OpcUa;

/// <summary>基于 OPC Foundation .NET SDK 的 OPC UA 客户端。</summary>
public sealed class OpcUaCommunication : CommunicationClientBase, IMessageClient<OpcUaMonitoredItemMessage>
{
    private readonly OpcUaConfig opcUaConfig;
    private readonly ISessionFactory sessionFactory;
    private readonly SemaphoreSlim sessionSemaphore = new(1, 1);
    private readonly HashSet<string> subscriptionNodeIds;
    private readonly Channel<OpcUaMonitoredItemMessage> messages;
    private ISession? opcUaSession;
    private Subscription? defaultSubscription;
    private long droppedMessageCount;

    /// <summary>初始化使用默认 OPC Foundation Session Factory 的客户端。</summary>
    public OpcUaCommunication(OpcUaConfig config)
        : this(config, new DefaultSessionFactory(DefaultTelemetry.Create(static _ => { }))) { }

    /// <summary>初始化使用指定 Session Factory 的客户端，主要用于高级配置和测试。</summary>
    public OpcUaCommunication(OpcUaConfig config, ISessionFactory sessionFactory)
        : this(CreateSnapshot(config), sessionFactory, true) { }

    private OpcUaCommunication(OpcUaConfig snapshot, ISessionFactory sessionFactory, bool _) : base(snapshot)
    {
        opcUaConfig = snapshot;
        this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        subscriptionNodeIds = new HashSet<string>(snapshot.SubscribeNodes, StringComparer.Ordinal);
        var options = new BoundedChannelOptions(snapshot.MessageBufferCapacity)
        {
            FullMode = ToChannelMode(snapshot.MessageOverflowStrategy),
            SingleReader = false,
            SingleWriter = false
        };
        messages = Channel.CreateBounded<OpcUaMonitoredItemMessage>(options, _ =>
            Interlocked.Increment(ref droppedMessageCount));
    }

    /// <inheritdoc />
    public event EventHandler<MessageReceivedEventArgs<OpcUaMonitoredItemMessage>>? MessageReceived;

    /// <summary>获取因缓冲区溢出而被丢弃的消息总数。</summary>
    public long DroppedMessageCount => Interlocked.Read(ref droppedMessageCount);

    /// <inheritdoc />
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ApplicationConfiguration appConfig = await CreateApplicationConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);
            EndpointDescription endpoint = await SelectConfiguredEndpointAsync(appConfig, cancellationToken)
                .ConfigureAwait(false);
            var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(appConfig));
            IUserIdentity identity = CreateIdentity();
            ISession openedSession = await sessionFactory.CreateAsync(
                appConfig, configuredEndpoint, false, opcUaConfig.ApplicationName,
                opcUaConfig.SessionTimeout, identity, null, cancellationToken).ConfigureAwait(false);

            opcUaSession = openedSession;
            openedSession.KeepAlive += SessionKeepAlive;
            defaultSubscription = new Subscription(openedSession.DefaultSubscription)
            {
                PublishingInterval = opcUaConfig.PublishingInterval,
                PublishingEnabled = true
            };
            openedSession.AddSubscription(defaultSubscription);
            await defaultSubscription.CreateAsync().ConfigureAwait(false);
            await RestoreSubscriptionsCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"OPC UA 连接失败：{ex.Message}", ex);
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        await sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ISession? current = opcUaSession;
            Subscription? subscription = defaultSubscription;
            opcUaSession = null;
            defaultSubscription = null;
            if (current is null) return;

            current.KeepAlive -= SessionKeepAlive;
            try
            {
                if (subscription is not null)
                {
                    foreach (MonitoredItem item in subscription.MonitoredItems)
                        item.Notification -= OnMonitoredItemNotification;
                    await subscription.DeleteAsync(true).ConfigureAwait(false);
                }
                await current.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                subscription?.Dispose();
                current.Dispose();
            }
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    /// <inheritdoc />
    protected override bool IsConnectionAlive()
    {
        try { return opcUaSession?.Connected == true; }
        catch (ObjectDisposedException) { return false; }
    }

    /// <inheritdoc />
    public ValueTask PublishAsync(OpcUaMonitoredItemMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Value is null) throw new ArgumentException("写入值不能为空。", nameof(message));
        return new ValueTask(WriteNodeAsync(message.NodeId, message.Value, cancellationToken));
    }

    /// <inheritdoc />
    public IAsyncEnumerable<OpcUaMonitoredItemMessage> ReadMessagesAsync(CancellationToken cancellationToken = default)
        => messages.Reader.ReadAllAsync(cancellationToken);

    /// <summary>读取指定节点并转换为目标类型。</summary>
    public async Task<T?> ReadNodeAsync<T>(string nodeId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        NodeId parsedNodeId = ParseNodeId(nodeId);
        await sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ISession session = RequireConnectedSession();
            var request = new ReadValueIdCollection
            {
                new ReadValueId { NodeId = parsedNodeId, AttributeId = Attributes.Value }
            };
            var response = await session.ReadAsync(null, 0, TimestampsToReturn.Both, request, cancellationToken)
                .ConfigureAwait(false);
            DataValue? value = response.Results?.FirstOrDefault();
            if (value is null || StatusCode.IsBad(value.StatusCode))
                throw new ServiceResultException(value?.StatusCode ?? StatusCodes.BadUnexpectedError);
            if (value.Value is T typed) return typed;
            return (T?)Convert.ChangeType(value.Value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"OPC UA 读取失败：{ex.Message}").ConfigureAwait(false);
            throw;
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    /// <summary>向指定节点写入值。</summary>
    public async Task WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(value);
        NodeId parsedNodeId = ParseNodeId(nodeId);
        await sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ISession session = RequireConnectedSession();
            var writes = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = parsedNodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                }
            };
            var response = await session.WriteAsync(null, writes, cancellationToken).ConfigureAwait(false);
            StatusCode status = response.Results?.FirstOrDefault() ?? StatusCodes.BadUnexpectedError;
            if (StatusCode.IsBad(status)) throw new ServiceResultException(status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"OPC UA 写入失败：{ex.Message}").ConfigureAwait(false);
            throw;
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    /// <summary>批量订阅节点；成功订阅的节点会在重连后自动恢复。</summary>
    public async Task SubscribeAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(nodeIds);
        string[] normalized = nodeIds.Select(static node => node?.Trim())
            .Where(static node => !string.IsNullOrWhiteSpace(node)).Cast<string>()
            .Distinct(StringComparer.Ordinal).ToArray();
        foreach (string node in normalized) _ = ParseNodeId(node);

        await sessionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = RequireConnectedSession();
            await AddSubscriptionsCoreAsync(normalized, cancellationToken).ConfigureAwait(false);
            subscriptionNodeIds.UnionWith(normalized);
        }
        finally
        {
            sessionSemaphore.Release();
        }
    }

    private async Task<ApplicationConfiguration> CreateApplicationConfigurationAsync(CancellationToken cancellationToken)
    {
        string root = opcUaConfig.PkiRootPath;
        var configuration = new ApplicationConfiguration
        {
            ApplicationName = opcUaConfig.ApplicationName,
            ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:{Uri.EscapeDataString(opcUaConfig.ApplicationName)}",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = "Directory", StorePath = Path.Combine(root, "own"),
                    SubjectName = $"CN={opcUaConfig.ApplicationName}"
                },
                TrustedIssuerCertificates = Store(Path.Combine(root, "issuer")),
                TrustedPeerCertificates = Store(Path.Combine(root, "trusted")),
                RejectedCertificateStore = Store(Path.Combine(root, "rejected")),
                AutoAcceptUntrustedCertificates = opcUaConfig.AutoAcceptUntrustedCertificates,
                RejectSHA1SignedCertificates = true
            },
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas { OperationTimeout = opcUaConfig.Timeout },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = (int)opcUaConfig.SessionTimeout }
        };
        await configuration.ValidateAsync(ApplicationType.Client, cancellationToken).ConfigureAwait(false);
        if (opcUaConfig.SecurityMode != MessageSecurityMode.None)
        {
            var application = new ApplicationInstance(configuration, sessionFactory.Telemetry);
            bool certificateReady = await application.CheckApplicationInstanceCertificatesAsync(
                false, 2048, cancellationToken).ConfigureAwait(false);
            if (!certificateReady)
                throw new InvalidOperationException("无法创建或加载 OPC UA 客户端应用证书。");
        }
        if (opcUaConfig.AutoAcceptUntrustedCertificates)
        {
            configuration.CertificateValidator.CertificateValidation += (_, args) =>
                args.Accept = args.Error.StatusCode == StatusCodes.BadCertificateUntrusted;
        }
        return configuration;
    }

    private static CertificateTrustList Store(string path) => new() { StoreType = "Directory", StorePath = path };

    private async Task<EndpointDescription> SelectConfiguredEndpointAsync(
        ApplicationConfiguration appConfig,
        CancellationToken cancellationToken)
    {
        using DiscoveryClient discovery = await DiscoveryClient.CreateAsync(
            appConfig,
            new Uri(opcUaConfig.EndpointUrl),
            DiagnosticsMasks.None,
            cancellationToken).ConfigureAwait(false);
        EndpointDescriptionCollection endpoints = await discovery.GetEndpointsAsync(
            new StringCollection(), cancellationToken).ConfigureAwait(false);
        return endpoints
            .Where(endpoint => endpoint.SecurityMode == opcUaConfig.SecurityMode
                && string.Equals(endpoint.SecurityPolicyUri, opcUaConfig.SecurityPolicy, StringComparison.Ordinal))
            .OrderByDescending(static endpoint => endpoint.SecurityLevel)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"服务器未提供安全配置 {opcUaConfig.SecurityMode}/{opcUaConfig.SecurityPolicy} 对应的端点。");
    }

    private IUserIdentity CreateIdentity()
        => opcUaConfig.UseAnonymousIdentity
            ? new UserIdentity(new AnonymousIdentityToken())
            : new UserIdentity(new UserNameIdentityToken
            {
                UserName = opcUaConfig.Username!,
                Password = Encoding.UTF8.GetBytes(opcUaConfig.Password ?? string.Empty)
            });

    private async Task RestoreSubscriptionsCoreAsync(CancellationToken cancellationToken)
        => await AddSubscriptionsCoreAsync(subscriptionNodeIds, cancellationToken).ConfigureAwait(false);

    private async Task AddSubscriptionsCoreAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken)
    {
        Subscription subscription = defaultSubscription
            ?? throw new InvalidOperationException("OPC UA 订阅尚未初始化。");
        bool changed = false;
        foreach (string nodeId in nodeIds)
        {
            if (subscription.MonitoredItems.Any(item => string.Equals(item.StartNodeId?.ToString(), nodeId, StringComparison.Ordinal)))
                continue;
            var item = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = nodeId, StartNodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value, SamplingInterval = opcUaConfig.PublishingInterval
            };
            item.Notification += OnMonitoredItemNotification;
            subscription.AddItem(item);
            changed = true;
        }
        if (changed) await subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private ISession RequireConnectedSession()
        => opcUaSession is { Connected: true } session
            ? session
            : throw new InvalidOperationException("OPC UA 会话未连接。");

    private static NodeId ParseNodeId(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return NodeId.Parse(nodeId);
    }

    private void SessionKeepAlive(ISession session, KeepAliveEventArgs args)
    {
        if (args.Status is null || !ServiceResult.IsNotGood(args.Status)) return;
        var exception = new ServiceResultException(args.Status);
        _ = HandleCommunicationFailureAsync(exception, $"OPC UA KeepAlive 失败：{args.Status}");
    }

    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs args)
    {
        try
        {
            foreach (DataValue value in item.DequeueValues())
            {
                if (StatusCode.IsBad(value.StatusCode)) continue;
                var message = new OpcUaMonitoredItemMessage(
                    item.StartNodeId.ToString(), value.Value, value.SourceTimestamp);
                _ = messages.Writer.TryWrite(message);
                EnqueueObserverNotification(() => MessageReceived?.Invoke(
                    this, new MessageReceivedEventArgs<OpcUaMonitoredItemMessage>(message)));
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex, $"处理 OPC UA 订阅消息失败：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await base.DisposeAsync().ConfigureAwait(false);
        messages.Writer.TryComplete();
        if (sessionSemaphore.Wait(0))
            sessionSemaphore.Dispose();
    }

    private static OpcUaConfig CreateSnapshot(OpcUaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.Validate()) throw new ArgumentException("OPC UA 配置无效。", nameof(config));
        return config.Snapshot();
    }

    private static BoundedChannelFullMode ToChannelMode(OpcUaMessageOverflowStrategy strategy)
        => strategy switch
        {
            OpcUaMessageOverflowStrategy.DropOldest => BoundedChannelFullMode.DropOldest,
            OpcUaMessageOverflowStrategy.DropNewest => BoundedChannelFullMode.DropNewest,
            OpcUaMessageOverflowStrategy.DropWrite => BoundedChannelFullMode.DropWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };
}
