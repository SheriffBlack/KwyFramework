using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// 用于 gRPC over Windows Named Pipe 的 Host 级 Kestrel 配置扩展。
/// </summary>
public static class GrpcNamedPipeExtensions
{
    /// <summary>
    /// 添加一个通过 Windows Named Pipe 监听的 HTTP/2 Kestrel 端点。
    /// 必须由可执行服务 Host 调用；Host 仍负责选择管道名称和配置 Windows 访问控制。
    /// </summary>
    public static IWebHostBuilder UseKwyGrpcNamedPipe(
        this IWebHostBuilder webHostBuilder,
        string pipeName,
        Action<NamedPipeTransportOptions>? configureTransport = null)
    {
        ArgumentNullException.ThrowIfNull(webHostBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("gRPC Named Pipe 传输仅支持 Windows。");

        if (configureTransport is not null)
            webHostBuilder.UseNamedPipes(configureTransport);

        return webHostBuilder.ConfigureKestrel(options =>
        {
            options.ListenNamedPipe(pipeName, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });
    }
}
