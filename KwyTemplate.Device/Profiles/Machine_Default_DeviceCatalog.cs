using Kwy.Communicate.Abstractions;
using Kwy.Communicate.NI;
using Kwy.Communicate.TcpSerial.Configs;
using Kwy.Device.Abstractions;
using Kwy.Device.Instruments.Lcr;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.PLCs.Hsl;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.Plcs;
using KwyTemplate.Device.Scanners;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// 默认设备清单：声明当前模板项目启动时需要创建哪些设备。
/// 少量固定项目可以直接写在 Catalog 中；同一机型下的小设备差异通过 Selection 配置交给 Factory 创建。
/// </summary>
public sealed class Machine_Default_DeviceCatalog : IDeviceCatalog
{
    private readonly IDeviceConfigProvider configProvider;

    public Machine_Default_DeviceCatalog(IDeviceConfigProvider configProvider)
    {
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    public string CatalogKey => nameof(Machine_Default_DeviceCatalog);

    public bool IsDefault => true;

    public IReadOnlyList<DeviceDefinition> CreateDeviceDefinitions()
    {

        // ─────────────────────────────────────────────────────────────────────
        // ★ 选择器
        // ─────────────────────────────────────────────────────────────────────
        DeviceSelectionConfig selection = configProvider.GetOrCreate(CatalogKey, ((IDeviceCatalog)this).SelectionConfigId, CreateDefaultSelectionConfig);
        
        HslPlcConfig mainPlcConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainPlc, CreateDefaultMainPlcConfig);
        DeviceDefinition dcrMeter1 = CreateDcrMeter(1, selection);
        BarcodeScannerConfig scannerConfig = configProvider.GetOrCreate(CatalogKey, DeviceIds.MainScanner, CreateDefaultBarcodeScannerConfig);

        return
        [
            new HslPlcDeviceDefinition(
                DeviceIds.MainPlc,
                "主 PLC",
                mainPlcConfig),

            new BarcodeScannerDeviceDefinition(
                DeviceIds.MainScanner,
                "Reel扫码枪",
                scannerConfig),

            dcrMeter1
        ];
    }

    private DeviceDefinition CreateDcrMeter(int index, DeviceSelectionConfig selection)
    {
        string deviceId = DeviceIds.Instrument("Dcr", index);
        var resolved = ResolveDcrMeter(index, deviceId, selection);
        IDeviceConfig parameterConfig = resolved.Model switch
        {
            DcrMeterModel.AdexDcr => configProvider.GetOrCreate(
                CatalogKey,
                $"{deviceId}.AdexDcr",
                static () => new AdexDcrConfig()),
            DcrMeterModel.HiokiLcr => configProvider.GetOrCreate(
                CatalogKey,
                $"{deviceId}.HiokiLcr",
                static () => new HiokiLcrConfig()),
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, null)
        };

        return DeviceDefinitionFactory.CreateDcrMeter(index, resolved.Model, resolved.ConnectionConfig, parameterConfig);
    }

    private DcrMeterResolution ResolveDcrMeter(int index, string deviceId, DeviceSelectionConfig selection)
    {
        if (selection.DcrMeter1Mode == DeviceSelectionMode.AutoDetect)
        {
            GpibConfig gpibConfig = configProvider.GetOrCreate(
                CatalogKey,
                $"{deviceId}.Gpib",
                CreateDefaultDcrGpibConfig);
            DcrMeterModel? detectedModel = GpibInstrumentAutoDetector.DetectDcrMeter(gpibConfig);
            if (detectedModel.HasValue)
            {
                return new DcrMeterResolution(detectedModel.Value, gpibConfig);
            }

            if (!selection.DcrMeter1FallbackToManual)
            {
                throw new InvalidOperationException($"DCR 电阻仪 {index} 自动识别失败，请检查 GPIB 地址或改为手动选择。");
            }
        }

        SerialPortConfig serialConfig = configProvider.GetOrCreate(
            CatalogKey,
            $"{deviceId}.Serial",
            CreateDefaultDcrSerialConfig);
        return new DcrMeterResolution(selection.DcrMeter1, serialConfig);
    }

    private static DeviceSelectionConfig CreateDefaultSelectionConfig()
        => new();

    private static HslPlcConfig CreateDefaultMainPlcConfig()
        => HslPlcConfigDefaults.CreateKeyenceKv8000MainPlc();

    private static BarcodeScannerConfig CreateDefaultBarcodeScannerConfig()
        => new();

    private static SerialPortConfig CreateDefaultDcrSerialConfig()
        => new()
        {
            Port = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            KeepAlive = false,
            AutoReconnect = false
        };

    private static GpibConfig CreateDefaultDcrGpibConfig()
        => new()
        {
            BoardNumber = 0,
            PrimaryAddress = 23,
            SecondaryAddress = 0,
            Timeout = 3000,
            KeepAlive = false,
            AutoReconnect = false,
            MaxReconnectAttempts = 0
        };

    private sealed record DcrMeterResolution(DcrMeterModel Model, IProtocolConfig ConnectionConfig);
}


