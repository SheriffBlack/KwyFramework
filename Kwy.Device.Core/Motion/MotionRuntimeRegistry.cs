using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

public sealed class MotionRuntimeRegistry : IMotionRuntimeRegistry
{
    private readonly IReadOnlyDictionary<string, IMotionDeviceRuntime> runtimes;

    public MotionRuntimeRegistry(IEnumerable<IMotionDeviceRuntime> runtimes)
    {
        ArgumentNullException.ThrowIfNull(runtimes);

        var byId = new Dictionary<string, IMotionDeviceRuntime>(StringComparer.OrdinalIgnoreCase);
        foreach (IMotionDeviceRuntime runtime in runtimes)
        {
            if (!byId.TryAdd(runtime.DeviceId, runtime))
            {
                throw new InvalidOperationException($"A motion runtime with DeviceId '{runtime.DeviceId}' is already registered.");
            }
        }

        this.runtimes = byId;
        Runtimes = byId.Values.ToArray();
    }

    public IReadOnlyCollection<IMotionDeviceRuntime> Runtimes { get; }

    public IMotionDeviceRuntime GetRequired(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return runtimes.TryGetValue(deviceId, out IMotionDeviceRuntime? runtime)
            ? runtime
            : throw new KeyNotFoundException($"Motion runtime '{deviceId}' is not registered.");
    }

    public IMotionDeviceRuntime GetRequiredSingle()
    {
        return Runtimes.Count switch
        {
            1 => Runtimes.First(),
            0 => throw new InvalidOperationException("No motion-card runtime is registered."),
            _ => throw new InvalidOperationException(
                "Multiple motion-card runtimes are registered. Resolve IMotionRuntimeRegistry and select one by DeviceId.")
        };
    }
}
