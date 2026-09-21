using Grpc.Core;
using Grpc.Net.Client;
using Kwy.Communicate.Core;
using Kwy.Communicate.Grpc.Client.Configs;
using Kwy.Communicate.Grpc.Client.Enums;
using Kwy.Communicate.Grpc.Contracts.V1;
using System.IO.Pipes;

namespace Kwy.Communicate.Grpc.Client;

/// <summary>
/// Owns one gRPC channel and integrates its lifecycle with Kwy communication clients.
/// Business RPCs remain on generated, strongly typed clients created from <see cref="CallInvoker"/>.
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
    /// Creates a generated or wrapped gRPC client over the connected channel.
    /// </summary>
    public TClient CreateClient<TClient>(Func<CallInvoker, TClient> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ThrowIfDisposed();

        if (!IsConnected || channel is null)
            throw new InvalidOperationException("The gRPC channel is not connected.");

        return factory(channel.CreateCallInvoker());
    }

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
        channel = CreateChannel();

        // gRPC Channel is lazy: an actual RPC is required to confirm that the endpoint is reachable.
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
            throw new InvalidOperationException("The gRPC channel has not been created.");

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
            _ => throw new NotSupportedException($"Unsupported gRPC transport: {grpcConfig.Transport}.")
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

        // The URI is required by GrpcChannel but isn't used to establish the pipe connection.
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = handler
        });
    }
}
