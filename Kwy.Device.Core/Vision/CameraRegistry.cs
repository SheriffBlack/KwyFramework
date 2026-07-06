using Kwy.Device.Abstractions.Vision;

namespace Kwy.Device.Core.Vision;

public sealed class CameraRegistry : ICameraRegistry
{
    private readonly IReadOnlyDictionary<string, ICameraDevice> cameras;

    public CameraRegistry(IEnumerable<ICameraDevice> cameras)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        var byId = new Dictionary<string, ICameraDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (ICameraDevice camera in cameras)
        {
            if (!byId.TryAdd(camera.DeviceId, camera))
            {
                throw new InvalidOperationException($"A camera with DeviceId '{camera.DeviceId}' is already registered.");
            }
        }

        this.cameras = byId;
        Cameras = byId.Values.ToArray();
    }

    public IReadOnlyCollection<ICameraDevice> Cameras { get; }

    public ICameraDevice GetRequired(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return cameras.TryGetValue(deviceId, out ICameraDevice? camera)
            ? camera
            : throw new KeyNotFoundException($"Camera '{deviceId}' is not registered.");
    }

    public TCapability GetRequiredCapability<TCapability>(string deviceId) where TCapability : class
    {
        ICameraDevice camera = GetRequired(deviceId);
        return camera as TCapability
            ?? throw new NotSupportedException($"Camera '{deviceId}' does not support {typeof(TCapability).Name}.");
    }
}
