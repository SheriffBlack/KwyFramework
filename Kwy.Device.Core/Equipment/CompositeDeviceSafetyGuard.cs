using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Core.Equipment;

public sealed class CompositeDeviceSafetyGuard : IDeviceSafetyGuard
{
    private readonly DeviceSafetyOptions options;
    private readonly IEnumerable<IDeviceSafetyParticipant> participants;

    public CompositeDeviceSafetyGuard(
        DeviceSafetyOptions options,
        IEnumerable<IDeviceSafetyParticipant> participants)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.participants = participants ?? throw new ArgumentNullException(nameof(participants));
    }

    public async Task<DeviceSafetyResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        List<DeviceSafetyViolation>? violations = null;

        foreach (var rule in options.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeviceSafetyViolation? violation = await rule(cancellationToken).ConfigureAwait(false);
            if (violation is not null)
            {
                (violations ??= new List<DeviceSafetyViolation>()).Add(violation);
            }
        }

        foreach (IDeviceSafetyParticipant participant in participants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeviceSafetyResult result = await participant.CheckAsync(cancellationToken).ConfigureAwait(false);
            foreach (DeviceSafetyViolation violation in result.Violations)
            {
                (violations ??= new List<DeviceSafetyViolation>()).Add(new DeviceSafetyViolation(
                    $"{participant.DeviceId}.{violation.Code}",
                    violation.Message));
            }
        }

        return violations is null ? DeviceSafetyResult.Allowed : new DeviceSafetyResult(violations);
    }
}
