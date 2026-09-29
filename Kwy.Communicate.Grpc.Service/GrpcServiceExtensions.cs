using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// 在 ASP.NET Core Host 中注册并映射 Kwy 基础 gRPC 服务端点。
/// </summary>
public static class GrpcServiceExtensions
{
    /// <summary>
    /// 注册 Kwy 基础 gRPC 设施及连通性端点依赖。
    /// 业务 RPC 服务必须由可执行 Host 另行注册。
    /// </summary>
    public static IServiceCollection AddKwyGrpc(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGrpc();
        return services;
    }

    public static IEndpointRouteBuilder MapKwyGrpcConnectivity(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGrpcService<GrpcConnectivityService>();
        return endpoints;
    }

}
