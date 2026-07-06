namespace Kwy.Device.Abstractions.Equipment;

public interface IDeviceSafetyGuard
{
    Task<DeviceSafetyResult> CheckAsync(CancellationToken cancellationToken = default);

    async Task CheckAndThrowAsync(CancellationToken cancellationToken = default)
    {
        DeviceSafetyResult result = await CheckAsync(cancellationToken);
        if (!result.IsAllowed)
        {
            throw new DeviceSafetyException(result.Violations);
        }
    }
}
