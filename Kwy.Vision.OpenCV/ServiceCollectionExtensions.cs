using Kwy.Vision.Abstractions;
using Kwy.Vision.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Vision.OpenCV;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyOpenCvVision(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        services.AddVisionBackend(new VisionBackendDescriptor(
            VisionBackendIds.OpenCv,
            "OpenCV",
            SupportsTraditionalVision: true,
            SupportsDeepLearning: false));
        return services;
    }
}
