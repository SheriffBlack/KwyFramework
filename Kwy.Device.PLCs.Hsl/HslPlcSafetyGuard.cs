using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.PLCs.Hsl;

public sealed class HslPlcSafetyGuard : IDeviceSafetyParticipant
{
    private readonly HslPlcDevice device;
    private readonly HslPlcRuntimeOptions options;

    public HslPlcSafetyGuard(HslPlcDevice device, HslPlcRuntimeOptions options)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string DeviceId => device.DeviceId;

    public async Task<DeviceSafetyResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!device.IsConnected)
        {
            return new DeviceSafetyResult(new[]
            {
                new DeviceSafetyViolation("HslPlc.NotConnected", "HSL PLC is not connected.")
            });
        }

        List<DeviceSafetyViolation>? violations = null;
        foreach (var point in options.SafetyPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool actual = await device.ReadBoolAsync(point.Address, cancellationToken);
            if (actual != point.ExpectedValue)
            {
                (violations ??= new List<DeviceSafetyViolation>()).Add(new DeviceSafetyViolation(
                    point.Code ?? $"HslPlc.{point.Name}",
                    point.Message ?? $"HSL PLC safety point '{point.Name}' is {actual}, expected {point.ExpectedValue}."));
            }
        }

        return violations is null ? DeviceSafetyResult.Allowed : new DeviceSafetyResult(violations);
    }
}
