using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// 在 ASP.NET Core 主机中注册并映射基线 Kwy gRPC 服务端点
/// </summary>
public static class KwyGrpcServiceExtensions
{
    public static IServiceCollection AddKwyGrpcServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGrpc();
        return services;
    }

    public static IEndpointRouteBuilder MapKwyGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGrpcService<KwyGrpcConnectivityService>();
        return endpoints;
    }
}
