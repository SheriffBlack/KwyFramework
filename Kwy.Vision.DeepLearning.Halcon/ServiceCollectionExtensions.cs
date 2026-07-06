using Kwy.Vision.Abstractions;
using Kwy.Vision.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Vision.DeepLearning.Halcon;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyHalconDeepLearning(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        services.AddVisionBackend(new VisionBackendDescriptor(
            VisionBackendIds.HalconDeepLearning,
            "HALCON Deep Learning",
            SupportsTraditionalVision: false,
            SupportsDeepLearning: true));
        return services;
    }
}
