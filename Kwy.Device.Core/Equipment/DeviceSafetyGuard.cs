using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class DeviceSafetyOptions
{
    public IList<Func<CancellationToken, Task<DeviceSafetyViolation?>>> Rules { get; }
        = new List<Func<CancellationToken, Task<DeviceSafetyViolation?>>>();
}

public sealed class DeviceSafetyGuard : IDeviceSafetyGuard
{
    private readonly DeviceSafetyOptions options;

    public DeviceSafetyGuard(DeviceSafetyOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DeviceSafetyResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (options.Rules.Count == 0)
        {
            return DeviceSafetyResult.Allowed;
        }

        List<DeviceSafetyViolation>? violations = null;
        foreach (var rule in options.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var violation = await rule(cancellationToken);
            if (violation is not null)
            {
                (violations ??= new List<DeviceSafetyViolation>()).Add(violation);
            }
        }

        return violations is null ? DeviceSafetyResult.Allowed : new DeviceSafetyResult(violations);
    }
}
