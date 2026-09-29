using Grpc.Core;
using Grpc.Net.Client;
using Kwy.Communicate.Core;
using Kwy.Communicate.Grpc.Client.Configs;
using Kwy.Communicate.Grpc.Client.Enums;
using Kwy.Communicate.Grpc.Contracts.V1;
using System.IO.Pipes;

namespace Kwy.Communicate.Grpc.Client;

/// <summary>
/// 持有一个 gRPC Channel，并将其生命周期接入 Kwy 通信客户端。
/// 业务 RPC 仍由基于 <see cref="CallInvoker"/> 创建的强类型生成客户端调用。
/// </summary>
public sealed class GrpcCommunication : CommunicationClientBase
{
    private readonly GrpcConfig grpcConfig;
    private GrpcChannel? channel;

    public GrpcCommunication(GrpcConfig config)
        : base(config)
    {
        grpcConfig = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 在已连接的 Channel 上创建生成的或封装后的 gRPC 客户端。
    /// </summary>
    public TClient CreateClient<TClient>(Func<CallInvoker, TClient> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ThrowIfDisposed();

        if (!IsConnected || channel is null)
            throw new InvalidOperationException("gRPC Channel 尚未连接。");

        return factory(channel.CreateCallInvoker());
    }

    /// <summary>
    /// 为一次业务 RPC 创建显式调用选项。
    /// 此方法不会重试 RPC；是否可重试必须由业务操作自行决定。
    /// </summary>
    /// <param name="deadline">本次 RPC 的最大执行时间，必须大于零。</param>
    /// <param name="cancellationToken">由调用方或 UI 生命周期持有的取消令牌。</param>
    /// <param name="headers">可选的单次调用元数据，例如关联标识。</param>
    public CallOptions CreateCallOptions(
        TimeSpan deadline,
        CancellationToken cancellationToken = default,
        Metadata? headers = null)
    {
        ThrowIfDisposed();

        if (deadline <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(deadline), "RPC Deadline 必须大于零。");

        return new CallOptions(
            headers: headers,
            deadline: DateTime.UtcNow.Add(deadline),
            cancellationToken: cancellationToken);
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
        channel = CreateChannel();

        // gRPC Channel 为惰性连接，必须通过一次实际 RPC 确认端点可达。
        await PingAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        GrpcChannel? previous = channel;
        channel = null;
        previous?.Dispose();
        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive() => channel is not null;

    protected override async Task<bool> CheckConnectionAliveAsync(CancellationToken cancellationToken)
    {
        if (channel is null)
            return false;

        try
        {
            await PingAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is RpcException or HttpRequestException)
        {
            return false;
        }
    }

    private async Task PingAsync(CancellationToken cancellationToken)
    {
        if (channel is null)
            throw new InvalidOperationException("gRPC Channel 尚未创建。");

        ConnectivityService.ConnectivityServiceClient client = new(channel);
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(grpcConfig.Timeout);
        await client.PingAsync(new PingRequest(), deadline: deadline, cancellationToken: cancellationToken)
            .ResponseAsync
            .ConfigureAwait(false);
    }

    private GrpcChannel CreateChannel()
        => grpcConfig.Transport switch
        {
            GrpcTransport.Tcp => GrpcChannel.ForAddress(grpcConfig.Endpoint),
            GrpcTransport.NamedPipe => CreateNamedPipeChannel(),
            _ => throw new NotSupportedException($"不支持的 gRPC 传输方式：{grpcConfig.Transport}。")
        };

    private GrpcChannel CreateNamedPipeChannel()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var pipe = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: grpcConfig.Endpoint,
                    direction: PipeDirection.InOut,
                    options: PipeOptions.Asynchronous);

                try
                {
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipe;
                }
                catch
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        };

        // GrpcChannel 需要 URI；它不参与 Named Pipe 的实际连接建立。
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = handler
        });
    }
}
