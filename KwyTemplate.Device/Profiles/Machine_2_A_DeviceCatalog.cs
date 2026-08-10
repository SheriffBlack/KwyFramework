using Kwy.Communicate.NI;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.IoCards.Advantech;
using Kwy.Device.PLCs.Hsl;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Instruments;
using KwyTemplate.Device.IoCards;
using KwyTemplate.Device.Plcs;
using KwyTemplate.Device.Scanners;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// Machine_2_A device catalog: main PLC, main IO card, Reel scanner and two ADEX DCR meters.
/// </summary>
public sealed class Machine_2_A_DeviceCatalog : IDeviceCatalog
{
    private readonly IDeviceConfigProvider configProvider;

    public Machine_2_A_DeviceCatalog(IDeviceConfigProvider configProvider)
    {
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    public string CatalogKey => nameof(Machine_2_A_DeviceCatalog);

    public IReadOnlyList<DeviceDefinition> CreateDeviceDefinitions()
    {
        HslPlcConfig mainPlcConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainPlc, CreateDefaultMainPlcConfig);
        AdvantechIoCardConfig mainIoConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainIoCard, CreateDefaultMainIoCardConfig);
        BarcodeScannerConfig scannerConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainScanner, CreateDefaultBarcodeScannerConfig);

        return
        [
            new HslPlcDeviceDefinition(
                DeviceIds.MainPlc,
                "主 PLC",
                mainPlcConfig),

            new AdvantechIoCardDeviceDefinition(
                DeviceIds.MainIoCard,
                "主 IO 卡",
                mainIoConfig),

            new BarcodeScannerDeviceDefinition(
                DeviceIds.MainScanner,
                "Reel扫码枪",
                scannerConfig),

            CreateAdexDcr(1, 23),
            CreateAdexDcr(2, 24)
        ];
    }

    private DeviceDefinition CreateAdexDcr(int index, byte primaryAddress)
    {
        string deviceId = DeviceIds.Instrument("Dcr", index);
        GpibConfig connectionConfig = configProvider.GetOrCreate(
            CatalogKey,
            $"{deviceId}.Gpib",
            () => CreateDefaultDcrGpibConfig(primaryAddress));
        AdexDcrConfig parameterConfig = configProvider.GetOrCreate(
            CatalogKey,
            $"{deviceId}.AdexDcr",
            static () => new AdexDcrConfig());

        return new AdexDcrDeviceDefinition(
            deviceId,
            $"DCR{index}",
            connectionConfig,
            parameterConfig);
    }

    private static HslPlcConfig CreateDefaultMainPlcConfig()
        => HslPlcConfigDefaults.CreateKeyenceKv8000MainPlc();

    private static BarcodeScannerConfig CreateDefaultBarcodeScannerConfig()
        => new();

    private static AdvantechIoCardConfig CreateDefaultMainIoCardConfig()
        => new()
        {
            DeviceDescription = "PCI-1730,BID#0",
            Model = "PCI-1730",
            DiPortCount = AdvantechIoCardConfig.MaxSupportedPorts,
            DoPortCount = AdvantechIoCardConfig.MaxSupportedPorts,
            EnableInterrupt = false
        };

    private static GpibConfig CreateDefaultDcrGpibConfig(byte primaryAddress)
        => new()
        {
            BoardNumber = 0,
            PrimaryAddress = primaryAddress,
            SecondaryAddress = 0,
            Timeout = 3000,
            KeepAlive = false,
            AutoReconnect = false,
            MaxReconnectAttempts = 0
        };
}

