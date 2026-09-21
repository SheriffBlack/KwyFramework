namespace Kwy.Communicate.Grpc.Client.Enums;

/// <summary>
/// Transport used by a gRPC channel.
/// </summary>
public enum GrpcTransport
{
    /// <summary>
    /// HTTP/2 over TCP. Supports local and remote endpoints.
    /// </summary>
    Tcp,

    /// <summary>
    /// Windows named-pipe IPC. Supports processes on the same machine only.
    /// </summary>
    NamedPipe
}
