using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.DeepLearning;
using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwy.Vision.Abstractions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKwyVisionAbstractions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IVisionAlgorithmRegistry, VisionAlgorithmRegistry>();
        services.TryAddSingleton<IVisionModelRegistry, VisionModelRegistry>();
        services.TryAddSingleton<IVisionBackendCatalog, VisionBackendCatalog>();
        services.TryAddSingleton<IVisionImageConverterRegistry, VisionImageConverterRegistry>();
        return services;
    }

    public static IServiceCollection AddVisionBackend(
        this IServiceCollection services,
        VisionBackendDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.BackendId);

        services.AddKwyVisionAbstractions();
        if (!services.Any(item => item.ServiceType == typeof(VisionBackendDescriptor)
            && item.ImplementationInstance is VisionBackendDescriptor existing
            && string.Equals(existing.BackendId, descriptor.BackendId, StringComparison.OrdinalIgnoreCase)))
        {
            services.AddSingleton(descriptor);
        }

        return services;
    }

    public static IServiceCollection AddVisionAlgorithm<TAlgorithm>(this IServiceCollection services)
        where TAlgorithm : class, IVisionAlgorithm
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        if (!services.Any(item => item.ServiceType == typeof(TAlgorithm)))
        {
            services.AddSingleton<TAlgorithm>();
            services.AddSingleton<IVisionAlgorithm>(provider => provider.GetRequiredService<TAlgorithm>());
        }

        return services;
    }

    public static IServiceCollection AddVisionModel<TModel>(this IServiceCollection services)
        where TModel : class, IVisionModel
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        if (!services.Any(item => item.ServiceType == typeof(TModel)))
        {
            services.AddSingleton<TModel>();
            services.AddSingleton<IVisionModel>(provider => provider.GetRequiredService<TModel>());
        }

        return services;
    }

    public static IServiceCollection AddVisionImageConverter<TConverter>(this IServiceCollection services)
        where TConverter : class, IVisionImageConverter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKwyVisionAbstractions();
        if (!services.Any(item => item.ServiceType == typeof(TConverter)))
        {
            services.AddSingleton<TConverter>();
            services.AddSingleton<IVisionImageConverter>(provider => provider.GetRequiredService<TConverter>());
        }

        return services;
    }
}
