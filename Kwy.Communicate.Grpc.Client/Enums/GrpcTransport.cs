namespace Kwy.Communicate.Grpc.Client.Enums;

/// <summary>
/// gRPC Channel 使用的传输方式。
/// </summary>
public enum GrpcTransport
{
    /// <summary>
    /// 基于 TCP 的 HTTP/2，支持本机与远程端点。
    /// </summary>
    Tcp,

    /// <summary>
    /// Windows Named Pipe 进程间通信，仅支持同一台机器上的进程。
    /// </summary>
    NamedPipe
}
