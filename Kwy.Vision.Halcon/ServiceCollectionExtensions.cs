using Kwy.Vision.Abstractions;
using Kwy.Vision.Abstractions.Runtime;
using Kwy.Vision.Halcon.Algorithms;
using Kwy.Vision.Halcon.Images;
using Kwy.Vision.Halcon.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Vision.Halcon;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyHalconVision(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        services.AddVisionBackend(new VisionBackendDescriptor(
            VisionBackendIds.Halcon,
            "HALCON",
            SupportsTraditionalVision: true,
            SupportsDeepLearning: false));
        services.AddVisionImageConverter<HalconVisionImageConverter>();
        services.TryAddSingleton<HalconShapeModelRepository>();
        services.TryAddSingleton<IHalconShapeModelRepository>(provider =>
            provider.GetRequiredService<HalconShapeModelRepository>());
        services.AddVisionAlgorithm<HalconBlobInspectionAlgorithm>();
        services.AddVisionAlgorithm<HalconShapeMatchingAlgorithm>();
        services.AddVisionAlgorithm<HalconEdgeMeasurementAlgorithm>();
        services.AddVisionAlgorithm<HalconPlanarCalibrationAlgorithm>();
        services.AddVisionAlgorithm<HalconContourDetectionAlgorithm>();
        services.AddVisionAlgorithm<HalconRotationCenterAlgorithm>();
        services.AddVisionAlgorithm<HalconFixtureAlgorithm>();
        services.AddVisionAlgorithm<HalconLineFittingAlgorithm>();
        services.AddVisionAlgorithm<HalconCircleFittingAlgorithm>();
        services.AddVisionAlgorithm<HalconDistanceMeasurementAlgorithm>();
        services.AddVisionAlgorithm<HalconCaliperGroupMeasurementAlgorithm>();
        services.AddVisionAlgorithm<HalconBlobFeatureInspectionAlgorithm>();
        services.AddVisionAlgorithm<HalconContourFittingAlgorithm>();
        services.AddVisionAlgorithm<HalconImagePreprocessAlgorithm>();
        services.AddVisionAlgorithm<HalconGeometryMeasurementAlgorithm>();
        services.AddVisionAlgorithm<HalconBarcodeReadAlgorithm>();
        services.AddVisionAlgorithm<HalconDataCode2DReadAlgorithm>();
        services.AddVisionAlgorithm<HalconLineMetrologyAlgorithm>();
        services.AddVisionAlgorithm<HalconCircleMetrologyAlgorithm>();
        return services;
    }
}
