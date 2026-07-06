namespace KwyTemplate.Flow.DeviceProfiles;

public sealed class MachineDeviceProfile
{
    public string Name { get; set; } = string.Empty;

    public List<MachineDeviceRequirement> Devices { get; set; } = [];

    public IReadOnlyList<string> GetRequiredDeviceIds()
        => Devices
            .Where(static device => device.Required)
            .Select(static device => device.DeviceId)
            .Where(static deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public void Validate()
    {
        foreach (MachineDeviceRequirement device in Devices)
        {
            if (string.IsNullOrWhiteSpace(device.Role))
            {
                throw new InvalidOperationException("Machine device role cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(device.DeviceId))
            {
                throw new InvalidOperationException($"Machine device '{device.Role}' device id cannot be empty.");
            }
        }

        string[] duplicatedRoles = Devices
            .GroupBy(static device => device.Role, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicatedRoles.Length > 0)
        {
            throw new InvalidOperationException($"Duplicated machine device roles: {string.Join(", ", duplicatedRoles)}.");
        }

        // DeviceId may be reused by multiple roles when one physical device
        // intentionally serves several business responsibilities.
    }
}
