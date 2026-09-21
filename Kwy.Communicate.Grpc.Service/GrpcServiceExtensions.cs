using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Communicate.Grpc.Service;

/// <summary>
/// Registers and maps the baseline Kwy gRPC service endpoints in an ASP.NET Core host.
/// </summary>
public static class GrpcServiceExtensions
{
    public static IServiceCollection AddGrpcServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddGrpc();
        return services;
    }

    public static IEndpointRouteBuilder MapGrpcServices(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGrpcService<GrpcConnectivityService>();
        return endpoints;
    }
}
