namespace KwyTemplate.Flow.DeviceProfiles;

public sealed class MachineDeviceRequirement
{
    public string Role { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Required { get; set; } = true;
}
