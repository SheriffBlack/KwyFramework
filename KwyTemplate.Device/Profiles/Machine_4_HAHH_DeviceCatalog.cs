using Kwy.Communicate.NI;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.Instruments.Lcr;
using Kwy.Device.IoCards.Advantech;
using Kwy.Device.PLCs.Hsl;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Instruments;
using KwyTemplate.Device.IoCards;
using KwyTemplate.Device.Plcs;
using KwyTemplate.Device.Scanners;
using KwyTemplate.Device.MarkPrinters;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// Machine_4_HAHH 设备清单：一台主 PLC、一张主 IO 卡、一个 HIOKI 3570、一个 ADEX DCR、两个 HIOKI 3533、一个 Reel 扫码枪。
/// HIOKI 3570/3533 都复用 HiokiLcr 通用驱动；连接配置按 CatalogKey + DeviceId 持久化到 JSON。
/// </summary>
public sealed class Machine_4_HAHH_DeviceCatalog : IDeviceCatalog
{
    private readonly IDeviceConfigProvider configProvider;

    public Machine_4_HAHH_DeviceCatalog(IDeviceConfigProvider configProvider)
    {
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    public string CatalogKey => nameof(Machine_4_HAHH_DeviceCatalog);

    public IReadOnlyList<DeviceDefinition> CreateDeviceDefinitions()
    {
        HslPlcConfig mainPlcConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainPlc, CreateDefaultMainPlcConfig);
        AdvantechIoCardConfig mainIoConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainIoCard, CreateDefaultMainIoCardConfig);
        BarcodeScannerConfig scannerConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainScanner, CreateDefaultBarcodeScannerConfig);
        MarkPrintConfig markPrintConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainMarkPrinter, CreateDefaultMarkPrintConfig);

        return
        [
            new HslPlcDeviceDefinition(
                DeviceIds.MainPlc,
                "\u4E3B PLC",
                mainPlcConfig),

            new AdvantechIoCardDeviceDefinition(
                DeviceIds.MainIoCard,
                "\u4E3B IO \u5361",
                mainIoConfig),

            new BarcodeScannerDeviceDefinition(
                DeviceIds.MainScanner,
                "Reel \u626B\u7801\u67AA",
                scannerConfig),

            new MarkPrintDeviceDefinition(
                DeviceIds.MainMarkPrinter,
                "\u7F16\u5E26\u6253\u5370\u673A",
                markPrintConfig),

            CreateHioki3570(1, 1),
            CreateAdexDcr(1, 2),
            CreateHioki3533(1, 3),
            CreateHioki3533(2, 4)
        ];
    }

    private DeviceDefinition CreateHioki3570(int index, byte primaryAddress)
        => CreateHiokiLcr("Ind", index, $"HIOKI 3570 LCR {index}", primaryAddress, CreateDefaultIndHiokiLcrConfig);

    private DeviceDefinition CreateHioki3533(int index, byte primaryAddress)
        => CreateHiokiLcr("Pol", index, $"HIOKI 3533 LCR {index}", primaryAddress, CreateDefaultPolHiokiLcrConfig);

    private DeviceDefinition CreateHiokiLcr(string model, int index, string deviceName, byte primaryAddress, Func<HiokiLcrConfig> createDefaultConfig)
    {
        string deviceId = DeviceIds.Instrument(model, index);
        GpibConfig connectionConfig = configProvider.GetOrCreate(
            CatalogKey,
            $"{deviceId}.Gpib",
            () => CreateDefaultGpibConfig(primaryAddress));
        HiokiLcrConfig parameterConfig = configProvider.GetOrCreate(
            CatalogKey,
            $"{deviceId}.HiokiLcr",
            createDefaultConfig);

        return new HiokiLcrDeviceDefinition(
            deviceId,
            deviceName,
            connectionConfig,
            parameterConfig);
    }

    /// <summary>
    /// Z、θ
    /// </summary>
    /// <returns></returns>
    private static HiokiLcrConfig CreateDefaultPolHiokiLcrConfig()
        => new()
        {
            LoadType = HiokiLcrLoadTypes.ZTheta
        };

    /// <summary>
    /// Ls、Rs
    /// </summary>
    /// <returns></returns>
    private static HiokiLcrConfig CreateDefaultIndHiokiLcrConfig()
        => new()
        {
            LoadType = HiokiLcrLoadTypes.LsRs
        };

    private DeviceDefinition CreateAdexDcr(int index, byte primaryAddress)
    {
        string deviceId = DeviceIds.Instrument("Dcr", index);
        GpibConfig connectionConfig = configProvider.GetOrCreate(
            CatalogKey,
            $"{deviceId}.Gpib",
            () => CreateDefaultGpibConfig(primaryAddress));
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

    private static MarkPrintConfig CreateDefaultMarkPrintConfig()
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

    private static GpibConfig CreateDefaultGpibConfig(byte primaryAddress)
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