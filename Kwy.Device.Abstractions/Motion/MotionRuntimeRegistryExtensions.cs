namespace Kwy.Device.Abstractions.Motion;

/// <summary>
/// 通过设备ID便捷地访问运动卡功能的辅助工具。
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
