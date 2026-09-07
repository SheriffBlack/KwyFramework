using Kwy.Communicate.NI;
using Kwy.Device.Abstractions;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.Instruments.Lcr;
using Kwy.Device.IoCards.Advantech;
using Kwy.Device.PLCs.Hsl;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Instruments;
using KwyTemplate.Device.IoCards;
using KwyTemplate.Device.Plcs;
using KwyTemplate.Device.Scanners;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// Converts declarative device profiles into the existing DeviceDefinition types.
/// It intentionally does not create a second registry or communication stack.
/// </summary>
public sealed class ConfigurableDeviceCatalog : IDeviceCatalog
{
    private readonly IDeviceConfigProvider configProvider;
    private readonly IMachineProfileProvider profileProvider;

    public ConfigurableDeviceCatalog(IDeviceConfigProvider configProvider, IMachineProfileProvider profileProvider)
    {
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        this.profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
    }

    public string CatalogKey => MachineRuntimeOptions.ConfigurableMachineKey;

    public IReadOnlyList<DeviceDefinition> CreateDeviceDefinitions()
    {
        MachineProfile profile = profileProvider.GetActiveProfile();
        return profile.Devices.Select(CreateDefinition).ToArray();
    }

    private DeviceDefinition CreateDefinition(MachineDeviceProfile profile)
    {
        string catalogKey = $"MachineProfile.{profileProvider.GetActiveProfile().ProfileKey}";
        string displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.DeviceId : profile.DisplayName;
        return profile.Kind switch
        {
            ConfigurableDeviceKind.MainPlc => new HslPlcDeviceDefinition(
                profile.DeviceId,
                displayName,
                configProvider.GetOrCreate(catalogKey, profile.DeviceId, HslPlcConfigDefaults.CreateKeyenceKv8000MainPlc)),
            ConfigurableDeviceKind.MainIoCard => new AdvantechIoCardDeviceDefinition(
                profile.DeviceId,
                displayName,
                configProvider.GetOrCreate(catalogKey, profile.DeviceId, CreateDefaultIoCardConfig)),
            ConfigurableDeviceKind.BarcodeScanner => new BarcodeScannerDeviceDefinition(
                profile.DeviceId,
                displayName,
                configProvider.GetOrCreate(catalogKey, profile.DeviceId, static () => new BarcodeScannerConfig())),
            ConfigurableDeviceKind.AdexDcr => new AdexDcrDeviceDefinition(
                profile.DeviceId,
                displayName,
                configProvider.GetOrCreate(catalogKey, $"{profile.DeviceId}.Gpib", () => CreateGpib(profile.PrimaryAddress)),
                configProvider.GetOrCreate(catalogKey, $"{profile.DeviceId}.AdexDcr", static () => new AdexDcrConfig())),
            ConfigurableDeviceKind.HiokiLcr => new HiokiLcrDeviceDefinition(
                profile.DeviceId,
                displayName,
                configProvider.GetOrCreate(catalogKey, $"{profile.DeviceId}.Gpib", () => CreateGpib(profile.PrimaryAddress)),
                configProvider.GetOrCreate(catalogKey, $"{profile.DeviceId}.HiokiLcr", static () => new HiokiLcrConfig())),
            _ => throw new NotSupportedException($"Configurable device kind '{profile.Kind}' is not supported yet.")
        };
    }

    private static GpibConfig CreateGpib(byte address)
        => new()
        {
            BoardNumber = 0,
            PrimaryAddress = address,
            SecondaryAddress = 0,
            Timeout = 3000,
            KeepAlive = false,
            AutoReconnect = false,
            MaxReconnectAttempts = 0
        };

    private static AdvantechIoCardConfig CreateDefaultIoCardConfig()
        => new()
        {
            DeviceDescription = "PCI-1730,BID#0",
            Model = "PCI-1730",
            DiPortCount = AdvantechIoCardConfig.MaxSupportedPorts,
            DoPortCount = AdvantechIoCardConfig.MaxSupportedPorts,
            EnableInterrupt = false
        };
}
