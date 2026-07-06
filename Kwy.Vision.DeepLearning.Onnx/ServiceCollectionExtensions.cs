using Kwy.Vision.Abstractions;
using Kwy.Vision.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kwy.Vision.DeepLearning.Onnx;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyOnnxVision(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        services.AddVisionBackend(new VisionBackendDescriptor(
            VisionBackendIds.Onnx,
            "ONNX Runtime",
            SupportsTraditionalVision: false,
            SupportsDeepLearning: true));
        return services;
    }
}
