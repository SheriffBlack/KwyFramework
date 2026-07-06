namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// Convenience accessors for resolving motion card capabilities by device id.
/// </summary>
public static class MotionRuntimeRegistryExtensions
{
    public static IMotionCard GetRequiredMotionCard(this IMotionRuntimeRegistry registry, string deviceId)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry.GetRequired(deviceId).Card;
    }

    public static IStandardMotionCard GetRequiredStandardMotionCard(this IMotionRuntimeRegistry registry, string deviceId)
    {
        return registry.GetRequiredCapability<IStandardMotionCard>(deviceId);
    }

    public static IAdvancedMotionCard GetRequiredAdvancedMotionCard(this IMotionRuntimeRegistry registry, string deviceId)
    {
        return registry.GetRequiredCapability<IAdvancedMotionCard>(deviceId);
    }

    public static TCapability GetRequiredCapability<TCapability>(this IMotionRuntimeRegistry registry, string deviceId)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(registry);

        var runtime = registry.GetRequired(deviceId);
        if (runtime.Card is TCapability capability)
        {
            return capability;
        }

        throw new InvalidOperationException(
            $"Motion device '{deviceId}' does not provide capability '{typeof(TCapability).Name}'.");
    }
}
