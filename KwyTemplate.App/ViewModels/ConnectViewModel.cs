using System.Collections.ObjectModel;
using System.Windows.Threading;
using Kwy.Communicate.NI;
using Kwy.Communicate.TcpSerial.Configs;
using Kwy.Device.Abstractions;
using Kwy.Device.IoCards.Advantech;
using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Device;
using KwyTemplate.Device.Connections.Editors;
using KwyTemplate.Device.Devices;
using KwyTemplate.Device.IoCards.Editors;
using KwyTemplate.Device.MarkPrinters;
using KwyTemplate.Device.Plcs.Editors;
using KwyTemplate.Device.Profiles;
using KwyTemplate.Device.Scanners;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;

namespace KwyTemplate.App.ViewModels;

public sealed class ConnectViewModel : BindableBase
{
    private static readonly string[] ConnectionConfigSuffixes = [".Gpib", ".Serial", ".Tcp"];
    private readonly IDeviceConfigProvider deviceConfigProvider;
    private readonly MachineBase machine;
    private readonly IMachineDeviceContext devices;
    private readonly ILocalizationService localizationService;
    private readonly Dispatcher dispatcher;
    private string statusMessage;

    public ConnectViewModel(
        IDeviceConfigProvider deviceConfigProvider,
        MachineBase machine,
        IMachineDeviceContext devices,
        ILocalizationService localizationService)
    {
        this.deviceConfigProvider = deviceConfigProvider ?? throw new ArgumentNullException(nameof(deviceConfigProvider));
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        statusMessage = localizationService.T("Connect.Status.Initial", "设备连接配置按 Config/{CatalogKey}/{DeviceId}.json 保存；修改参数后请重新连接对应设备。");
        dispatcher = Dispatcher.CurrentDispatcher;
        this.localizationService.LanguageChanged += OnLanguageChanged;
        ReloadConfigSources();
    }

    public ObservableCollection<ConnectionConfigSourceItem> ConfigSources { get; } = [];

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    private AsyncDelegateCommand? saveCommand;

    public AsyncDelegateCommand SaveCommand => saveCommand ??= new AsyncDelegateCommand(ExecuteSaveAsync);

    private AsyncDelegateCommand? reloadCommand;

    public AsyncDelegateCommand ReloadCommand => reloadCommand ??= new AsyncDelegateCommand(ExecuteReloadAsync);

    private async Task ExecuteSaveAsync()
    {
        await deviceConfigProvider.SaveAsync(DestroyToken).ConfigureAwait(false);
        StatusMessage = localizationService.T("Connect.Status.Saved", "设备连接配置已保存到 Config/{CatalogKey}；下次启动会自动加载。");
    }

    private async Task ExecuteReloadAsync()
    {
        await deviceConfigProvider.ReloadAsync(DestroyToken).ConfigureAwait(false);
        RunOnUi(() =>
        {
            ReloadConfigSources();
            StatusMessage = localizationService.T("Connect.Status.Reloaded", "设备连接配置已从 Config/{CatalogKey} 重载。");
        });
    }

    private void ReloadConfigSources()
    {
        ConfigSources.Clear();
        List<ConnectionConfigSourceItem> items = [];
        foreach (DeviceConfigEntry entry in deviceConfigProvider.GetEntries().Where(IsConnectionConfigEntry))
        {
            DeviceConfigDisplayInfo displayInfo = CreateDisplayInfo(entry);
            items.Add(new ConnectionConfigSourceItem(displayInfo.Header, CreateEditorSources(entry), displayInfo.SortGroup, displayInfo.SortOrder, displayInfo.DeviceName));
        }

        foreach (ConnectionConfigSourceItem item in items
                     .OrderBy(static item => item.SortGroup)
                     .ThenBy(static item => item.SortOrder)
                     .ThenBy(static item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(static item => item.Header, StringComparer.OrdinalIgnoreCase))
        {
            ConfigSources.Add(item);
        }
    }

    private DeviceConfigDisplayInfo CreateDisplayInfo(DeviceConfigEntry entry)
    {
        string deviceId = GetOwnerDeviceId(entry.DeviceId);
        IDevice? device = devices.Devices.FirstOrDefault(item => string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        string deviceName = ResolveDeviceName(deviceId, device?.DeviceName);

        TestStationModel? station = machine.TestStations.FirstOrDefault(item => item.InstrumentDeviceIds.Any(
            instrumentDeviceId => string.Equals(instrumentDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)));

        return station == null
            ? new DeviceConfigDisplayInfo(deviceName, 1, int.MaxValue, deviceName)
            : new DeviceConfigDisplayInfo($"{ResolveStationName(station)} - {deviceName}", 0, station.StationId, deviceName);
    }

    private string ResolveDeviceName(string deviceId, string? fallback)
    {
        string defaultName = string.IsNullOrWhiteSpace(fallback) ? deviceId : fallback;
        return deviceId switch
        {
            DeviceIds.MainPlc => localizationService.T("Device.MainPlc", defaultName),
            DeviceIds.MainIoCard => localizationService.T("Device.MainIoCard", defaultName),
            DeviceIds.MainScanner => localizationService.T("Device.MainScanner", defaultName),
            DeviceIds.MainMarkPrinter => localizationService.T("Device.MainMarkPrinter", defaultName),
            _ => defaultName
        };
    }

    private string ResolveStationName(TestStationModel station)
        => string.IsNullOrWhiteSpace(station.StationNameKey)
            ? station.StationName
            : localizationService.T(station.StationNameKey, station.StationName);

    private void OnLanguageChanged(object? sender, LanguageType languageType)
        => RunOnUi(ReloadConfigSources);

    private static string GetOwnerDeviceId(string configDeviceId)
    {
        foreach (string suffix in ConnectionConfigSuffixes)
        {
            if (configDeviceId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return configDeviceId[..^suffix.Length];
            }
        }

        return configDeviceId;
    }

    private void RunOnUi(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static bool IsConnectionConfigEntry(DeviceConfigEntry entry)
        => entry.Config is Kwy.Device.PLCs.Hsl.HslPlcConfig
            or GpibConfig
            or SerialPortConfig
            or TcpConfig
            or BarcodeScannerConfig
            or MarkPrintConfig
            or AdvantechIoCardConfig;

    private static IReadOnlyList<object> CreateEditorSources(DeviceConfigEntry entry)
        => entry.Config switch
        {
            Kwy.Device.PLCs.Hsl.HslPlcConfig plcConfig => new HslPlcConfigEditorModel(plcConfig).CreatePropertyGridSources(),
            GpibConfig gpibConfig => [new GpibConnectionEditorModel(gpibConfig)],
            SerialPortConfig serialConfig => [CreateSerialEditor(serialConfig)],
            TcpConfig tcpConfig => [CreateTcpEditor(tcpConfig)],
            BarcodeScannerConfig scannerConfig => [CreateSerialEditor(scannerConfig.Serial)],
            MarkPrintConfig markPrintConfig => [CreateTcpEditor(markPrintConfig.Tcp)],
            AdvantechIoCardConfig ioCardConfig => [new AdvantechIoCardConfigEditorModel(ioCardConfig)],
            _ => [entry.Config]
        };

    private static TcpConnectionEditorModel CreateTcpEditor(TcpConfig config)
        => new(
            () => config.Host,
            value => config.Host = value,
            () => config.Port,
            value => config.Port = value,
            () => config.Timeout,
            value => config.Timeout = value,
            () => config.ReceiveTimeout,
            value => config.ReceiveTimeout = value,
            () => config.SendTimeout,
            value => config.SendTimeout = value);

    private static SerialConnectionEditorModel CreateSerialEditor(SerialPortConfig config)
        => new(
            () => config.Port,
            value => config.Port = value,
            () => config.BaudRate,
            value => config.BaudRate = value,
            () => config.DataBits,
            value => config.DataBits = value,
            () => config.Parity,
            value => config.Parity = value,
            () => config.StopBits,
            value => config.StopBits = value);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
        }

        base.Dispose(disposing);
    }
}

public sealed record ConnectionConfigSourceItem(string Header, IReadOnlyList<object> Sources, int SortGroup, int SortOrder, string DeviceName);

internal sealed record DeviceConfigDisplayInfo(string Header, int SortGroup, int SortOrder, string DeviceName);
