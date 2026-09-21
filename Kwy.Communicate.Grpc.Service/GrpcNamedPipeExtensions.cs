using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// Host-level Kestrel configuration helpers for gRPC over Windows named pipes.
/// </summary>
public static class GrpcNamedPipeExtensions
{
    /// <summary>
    /// Adds an HTTP/2 Kestrel endpoint that listens on a Windows named pipe.
    /// This must be called by the executable service host, which remains responsible
    /// for choosing the pipe name and configuring Windows access control.
    /// </summary>
    public static IWebHostBuilder UseGrpcNamedPipe(
        this IWebHostBuilder webHostBuilder,
        string pipeName,
        Action<NamedPipeTransportOptions>? configureTransport = null)
    {
        ArgumentNullException.ThrowIfNull(webHostBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("gRPC named-pipe transport is supported on Windows only.");

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
