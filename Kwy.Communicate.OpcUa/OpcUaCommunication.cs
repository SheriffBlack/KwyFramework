using Kwy.Communicate.Abstractions;
using Kwy.Communicate.Abstractions.Events;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Core;
using Opc.Ua;
using Opc.Ua.Client;
using System.Data;
using System.Text;
using System.Threading.Channels;
using ConnectionState = Kwy.Communicate.Abstractions.Enums.ConnectionState;

namespace Kwy.Communicate.OpcUa;

/// <summary>
/// OPC UA官方底层通信协议实现
/// 依赖包: OPCFoundation.NetStandard.Opc.Ua.Client == 1.5.378.134
/// </summary>
public class OpcUaCommunication : CommunicationClientBase, IMessageClient<OpcUaMonitoredItemMessage>
{
    private readonly OpcUaConfig opcUaConfig;
    private readonly ISessionFactory sessionFactory; // ✨ 新增工厂字段
    private Session? opcUaSession;
    private Subscription? defaultSubscription;
    private readonly SemaphoreSlim subscriptionSemaphore = new(1, 1);
    private readonly Channel<OpcUaMonitoredItemMessage> messages;

    public event EventHandler<MessageReceivedEventArgs<OpcUaMonitoredItemMessage>>? MessageReceived;

    public OpcUaCommunication(OpcUaConfig config, ISessionFactory sessionFactory) : base(config)
    {
        opcUaConfig = config ?? throw new ArgumentNullException(nameof(config));
        this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

        var channelOptions = new BoundedChannelOptions(opcUaConfig.MessageBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };
        messages = Channel.CreateBounded<OpcUaMonitoredItemMessage>(channelOptions);
    }

    // 替换 ConnectInternalAsync 方法中已过时的 SelectEndpoint 调用为异步 SelectEndpointAsync
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        if (!opcUaConfig.Validate())
            throw new InvalidOperationException("OPC UA配置无效");

        try
        {
            // 1. 构建应用配置
            var appConfig = new ApplicationConfiguration
            {
                ApplicationName = opcUaConfig.ApplicationName,
                ApplicationUri = string.Format("urn:{0}:{1}", System.Net.Dns.GetHostName(), opcUaConfig.ApplicationName),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault" },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = opcUaConfig.AutoAcceptUntrustedCertificates,
                    RejectSHA1SignedCertificates = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = opcUaConfig.Timeout },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = (int)opcUaConfig.SessionTimeout }
            };

            await appConfig.ValidateAsync(ApplicationType.Client, cancellationToken);

            // 自动接受不受信任的证书（开发与内网环境必备）
            if (appConfig.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                appConfig.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = true; };
            }

            // 2. 选择端点（使用异步API）
            bool useSecurity = !string.Equals(opcUaConfig.SecurityPolicy, "None", StringComparison.OrdinalIgnoreCase);
            // 关键修正：传递 ITelemetryContext 参数，避免使用已过时的重载
            ITelemetryContext telemetry = null!; // 显式忽略可空警告，OPC UA SDK 底层允许 telemetry 为 null
            var selectedEndpoint = await CoreClientUtils.SelectEndpointAsync(
                appConfig,
                opcUaConfig.EndpointUrl,
                useSecurity,
                opcUaConfig.Timeout,
                telemetry,
                cancellationToken
            );
            if (selectedEndpoint == null)
                throw new InvalidOperationException("未能发现有效的OPC UA端点");

            var endpointConfiguration = EndpointConfiguration.Create(appConfig);
            var configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            // 3. 构建身份验证凭据
            IUserIdentity userIdentity = opcUaConfig.UseAnonymousIdentity
                ? new UserIdentity(new AnonymousIdentityToken())
                : new UserIdentity(new UserNameIdentityToken
                {
                    UserName = opcUaConfig.Username ?? string.Empty,
                    Password = string.IsNullOrEmpty(opcUaConfig.Password) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(opcUaConfig.Password)
                });

            // 4. 创建 Session (使用原始异步 Create 接口并传递 cancellationToken)
            // 注意: Create 已被标记为 Obsolete 建议使用 ISessionFactory.CreateAsync
            // 但为兼容此项目及简化重载选择，使用现有 Create(...) 重载
            // ✨ 直接使用注入的工厂创建会话
            opcUaSession = (Session)await this.sessionFactory.CreateAsync(
                appConfig,
                configuredEndpoint,
                updateBeforeConnect: false,
                sessionName: opcUaConfig.ApplicationName,
                sessionTimeout: (uint)opcUaConfig.SessionTimeout,
                identity: userIdentity,
                preferredLocales: null,
                ct: cancellationToken
            );

            // 5. 绑定心跳检测 (掉线重连的灵魂)
            opcUaSession.KeepAlive -= Session_KeepAlive;
            opcUaSession.KeepAlive += Session_KeepAlive;

            // 6. 初始化全局默认订阅器
            defaultSubscription = new Subscription(opcUaSession.DefaultSubscription)
            {
                PublishingInterval = opcUaConfig.PublishingInterval > 0 ? opcUaConfig.PublishingInterval : 100,
                PublishingEnabled = true
            };
            opcUaSession.AddSubscription(defaultSubscription);
            await defaultSubscription.CreateAsync();

            // 7. 重连或初次连接时，自动恢复挂载需要订阅的节点
            if (opcUaConfig.SubscribeNodes != null && opcUaConfig.SubscribeNodes.Count > 0)
            {
                // 拷贝一份防止并发修改
                var nodesToRecover = opcUaConfig.SubscribeNodes.ToList();
                opcUaConfig.SubscribeNodes.Clear(); // 清空后重新走 SubscribeAsync 注册流程
                await SubscribeAsync(nodesToRecover, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"OPC UA连接失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// OPC UA 断线检测回调
    /// </summary>
    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // ServiceResult.IsNotGood 在 1.5.x 中用于判断心跳是否丢失
        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            // 修正：ISession 没有 CurrentState 属性，改为 e.CurrentState
            OnErrorOccurred(new Exception($"OPC UA 通信中断: {e.Status}, State: {e.CurrentState}"), "KeepAlive 失败");

            if (!IsConnectionAlive())
                _ = HandleCommunicationFailureAsync(new Exception($"OPC UA communication interrupted: {e.Status}"), "KeepAlive failed");
        }
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (opcUaSession != null)
        {
            try
            {
                opcUaSession.KeepAlive -= Session_KeepAlive;
                if (defaultSubscription != null)
                {
                    await defaultSubscription.DeleteAsync(true);
                    defaultSubscription = null;
                }

                await opcUaSession.CloseAsync();
                opcUaSession.Dispose();
            }
            catch
            {
                // 忽略主动断开时的资源释放异常
            }
            finally
            {
                opcUaSession = null;
            }
        }
        await Task.CompletedTask;
    }

    protected override bool IsConnectionAlive()
    {
        return opcUaSession != null && opcUaSession.Connected;
    }

    public async ValueTask PublishAsync(OpcUaMonitoredItemMessage message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.NodeId))
            throw new ArgumentException("NodeId cannot be empty.", nameof(message));
        if (message.Value is null)
            throw new ArgumentException("Value cannot be null.", nameof(message));

        await WriteNodeAsync(message.NodeId, message.Value, cancellationToken);
    }

    public IAsyncEnumerable<OpcUaMonitoredItemMessage> ReadMessagesAsync(CancellationToken cancellationToken = default)
        => messages.Reader.ReadAllAsync(cancellationToken);

    #region OPC UA 特有节点读写 (原生 1.5.378 Async API)

    /// <summary>
    /// 强类型读取 OPC UA 节点
    /// </summary>
    public async Task<T?> ReadNodeAsync<T>(string nodeId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (opcUaSession == null || !opcUaSession.Connected)
                throw new InvalidOperationException("OPC UA 会话未连接");

            var nodeToRead = new ReadValueId
            {
                NodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value
            };
            var readIds = new ReadValueIdCollection { nodeToRead };

            // 1.5.378 原生异步读取
            var response = await opcUaSession.ReadAsync(
                null,
                0,
                TimestampsToReturn.Both, // 建议用 Both，部分 PLC 严格要求时间戳返回
                readIds,
                cancellationToken);

            var results = response.Results;
            if (results == null || results.Count == 0 || StatusCode.IsBad(results[0].StatusCode))
                return default;

            try
            {
                // 尝试进行类型转换
                return (T?)results[0].Value;
            }
            catch (InvalidCastException)
            {
                return default;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"OPC UA read failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 强类型写入 OPC UA 节点
    /// </summary>
    public async Task WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        try
        {
            if (opcUaSession == null || !opcUaSession.Connected)
                throw new InvalidOperationException("OPC UA 会话未连接");

            var writeValue = new WriteValue
            {
                NodeId = NodeId.Parse(nodeId),
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(value))
            };
            var writes = new WriteValueCollection { writeValue };

            // 1.5.378 原生异步写入
            var response = await opcUaSession.WriteAsync(
                null,
                writes,
                cancellationToken);

            var results = response.Results;

            if (results == null || results.Count == 0)
                throw new InvalidOperationException("写入操作未返回结果");

            if (StatusCode.IsBad(results[0]))
                throw new InvalidOperationException($"写入节点 {nodeId} 失败，状态码: {results[0]}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleCommunicationFailureAsync(ex, $"OPC UA write failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region 订阅功能

    /// <summary>
    /// 批量订阅节点变化
    /// </summary>
    public async Task SubscribeAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        if (disposed || !IsConnected || opcUaSession == null || defaultSubscription == null)
            throw new InvalidOperationException("会话未准备好，无法订阅");

        await subscriptionSemaphore.WaitAsync(cancellationToken);
        try
        {
            bool hasChanges = false;
            foreach (var nodeId in nodeIds.Distinct())
            {
                // 避免重复订阅
                if (defaultSubscription.MonitoredItems.Any(m => m.StartNodeId.ToString() == nodeId))
                    continue;

                var monitoredItem = new MonitoredItem(defaultSubscription.DefaultItem)
                {
                    DisplayName = nodeId,
                    StartNodeId = NodeId.Parse(nodeId),
                    AttributeId = Attributes.Value,
                    SamplingInterval = 50 // 50ms 底层采样率
                };

                // 绑定事件
                monitoredItem.Notification += OnMonitoredItemNotification;
                defaultSubscription.AddItem(monitoredItem);
                hasChanges = true;

                // 记录到 Config 中，以便断线重连时恢复
                if (!opcUaConfig.SubscribeNodes.Contains(nodeId))
                    opcUaConfig.SubscribeNodes.Add(nodeId);
            }

            if (hasChanges)
            {
                // 通知服务器应用订阅变更
                await defaultSubscription.ApplyChangesAsync();
            }
        }
        finally
        {
            subscriptionSemaphore.Release();
        }
    }

    /// <summary>
    /// 底层订阅数据变化回调，统一封装为强类型消息流。
    /// </summary>
    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            // DequeueValues 获取该节点自上次推送以来的所有变化值
            foreach (var value in item.DequeueValues())
            {
                if (StatusCode.IsBad(value.StatusCode)) continue;

                var monitoredItemMessage = new OpcUaMonitoredItemMessage(
                    item.StartNodeId.ToString(),
                    value.Value,
                    value.SourceTimestamp
                );
                messages.Writer.TryWrite(monitoredItemMessage);
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs<OpcUaMonitoredItemMessage>(monitoredItemMessage));
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred(ex, "处理 OPC UA 订阅数据异常");
        }
    }

    #endregion

    public override async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await base.DisposeAsync();
        messages.Writer.TryComplete();
        subscriptionSemaphore?.Dispose();
    }

    public override void Dispose()
    {
        if (disposed) return;
        base.Dispose();
        messages.Writer.TryComplete();
        subscriptionSemaphore?.Dispose();
    }
}


/// <summary>
/// OPC UA 节点地址字典 (与 PLC 工程师对齐的契约)
/// </summary>
public static class OpcTags
{
    // === 触发信号 (PLC -> PC) ===
    public const string VisionTrigger = "ns=2;s=Station.Vision.Trigger";
    public const string TestStation1_2_Trigger = "ns=2;s=Station.Test1.Trigger";

    // === 状态监控 (PLC -> PC) ===
    public const string MachineRunning = "ns=2;s=Machine.Status.Running";
    public const string ErrorAlarm = "ns=2;s=Machine.Status.Alarm";

    // === 握手信号 (PC -> PLC) ===
    public const string VisionDealOver = "ns=2;s=Station.Vision.Done";
    public const string TestDealOver = "ns=2;s=Station.Test1.Done";
}
