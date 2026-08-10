using System.Text.Json;

namespace KwyTemplate.Device.Profiles;

public interface IMachineProfileProvider
{
    MachineProfile GetActiveProfile();
}

public sealed class MachineProfileProvider : IMachineProfileProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly IMachineRuntimeOptionsProvider runtimeOptions;
    private readonly Lazy<MachineProfile> activeProfile;

    public MachineProfileProvider(IMachineRuntimeOptionsProvider runtimeOptions)
    {
        this.runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
        activeProfile = new Lazy<MachineProfile>(LoadActiveProfile);
    }

    public MachineProfile GetActiveProfile() => activeProfile.Value;

    private MachineProfile LoadActiveProfile()
    {
        string key = runtimeOptions.Get().ActiveProfileKey;
        string path = Path.Combine(AppContext.BaseDirectory, "Config", key, "MachineProfile.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            MachineProfile? loaded = JsonSerializer.Deserialize<MachineProfile>(File.ReadAllText(path), JsonOptions);
            if (loaded != null)
            {
                MachineProfileValidator.Validate(loaded);
                return loaded;
            }
        }

        MachineProfile defaults = CreateDefault(key);
        File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
        return defaults;
    }

    private static MachineProfile CreateDefault(string key)
        => new()
        {
            ProfileKey = key,
            Devices =
            [
                new() { DeviceId = DeviceIds.MainPlc, DisplayName = "主 PLC", Kind = ConfigurableDeviceKind.MainPlc },
                new() { DeviceId = DeviceIds.MainScanner, DisplayName = "Reel扫码枪", Kind = ConfigurableDeviceKind.BarcodeScanner },
                new() { DeviceId = DeviceIds.Instrument("Dcr", 1), DisplayName = "DCR1", Kind = ConfigurableDeviceKind.AdexDcr, PrimaryAddress = 2 }
            ],
            Stations =
            [
                new()
                {
                    StationId = 1,
                    StationName = "DCR1",
                    InstrumentDeviceIds = [DeviceIds.Instrument("Dcr", 1)],
                    TestNames = ["DCR"],
                    Operations = ["Check"]
                }
            ]
        };
}

public static class MachineProfileValidator
{
    public static void Validate(MachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Devices.GroupBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase).Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
        {
            throw new InvalidOperationException("Machine profile contains empty or duplicated device ids.");
        }

        if (profile.Stations.GroupBy(item => item.StationId).Any(group => group.Key <= 0 || group.Count() > 1))
        {
            throw new InvalidOperationException("Machine profile contains invalid or duplicated station ids.");
        }

        var deviceIds = profile.Devices.Select(item => item.DeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (profile.IoPoints.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Any(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1 || group.Any(item => item.Channel < 0)))
        {
            throw new InvalidOperationException("Machine profile contains invalid or duplicated IO point keys.");
        }

        var ioPoints = profile.IoPoints.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        foreach (MachineStationProfile station in profile.Stations)
        {
            if (station.InstrumentDeviceIds.Any(deviceId => !deviceIds.Contains(deviceId)))
            {
                throw new InvalidOperationException($"Station {station.StationId} references a device that is not declared in the profile.");
            }

            ValidateIoReference(station.Io.TestFinishedInputPoint, MachineIoPointDirection.Input, ioPoints, station.StationId);
            ValidateIoReference(station.Io.ResultOkInputPoint, MachineIoPointDirection.Input, ioPoints, station.StationId);
            ValidateIoReference(station.Io.ResultNgInputPoint, MachineIoPointDirection.Input, ioPoints, station.StationId);
            ValidateIoReference(station.Io.ResultReadCompletedOutputPoint, MachineIoPointDirection.Output, ioPoints, station.StationId);
            ValidateIoReference(station.Io.ResultOkOutputPoint, MachineIoPointDirection.Output, ioPoints, station.StationId);
            ValidateIoReference(station.Io.ResultNgOutputPoint, MachineIoPointDirection.Output, ioPoints, station.StationId);
        }
    }

    private static void ValidateIoReference(
        string? key,
        MachineIoPointDirection expectedDirection,
        IReadOnlyDictionary<string, MachineIoPointProfile> points,
        int stationId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!points.TryGetValue(key, out MachineIoPointProfile? point) || point.Direction != expectedDirection)
        {
            throw new InvalidOperationException($"Station {stationId} references invalid {expectedDirection} IO point '{key}'.");
        }
    }
}
