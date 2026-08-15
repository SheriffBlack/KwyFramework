using System.ComponentModel;
using System.Globalization;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.Instrument;
using Kwy.MVVM.Core;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using KwyTemplate.App.Models;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;

using KwyTemplate.Contracts.Localization;
namespace KwyTemplate.App.ViewModels;

public class CorrectionViewModel : BindableBase
{
    private readonly IRegionManager regionManager;
    private readonly ICorrectionParameterProvider correctionParameterProvider;
    private readonly MachineBase machine;
    private readonly IMachineDeviceContext devices;
    private readonly IAppNotificationService notificationService;
    private readonly ILocalizationService localizationService;
    private readonly MesConnectionStatus mesConnectionStatus;
    private AsyncDelegateCommand? ensureDefaultContentCommand;
    private AsyncDelegateCommand? executeOpenCorrectionCommand;
    private AsyncDelegateCommand? executeShortCorrectionCommand;
    private AsyncDelegateCommand? executeLoadCorrectionCommand;
    private bool defaultContentNavigated;
    private bool isNavigatingDefaultContent;
    private string frequency = string.Empty;
    private string frequencyUnit = string.Empty;
    private string voltage = "1";
    private string voltageUnit = string.Empty;
    private string loadType = string.Empty;
    private IReadOnlyList<string> loadTypeItems = [];
    private string dcLoad = string.Empty;
    private string lsStandardValue = string.Empty;
    private string lsStandardUnit = string.Empty;
    private string rsStandardValue = string.Empty;
    private string rsStandardUnit = string.Empty;
    private string lsCorrectionValue = string.Empty;
    private string rsCorrectionValue = string.Empty;
    private bool isCorrectionBusy;
    private bool disposed;
    private static readonly TimeSpan CorrectionReadTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CorrectionReadRetryInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan OpenCorrectionExecuteDelay = TimeSpan.Zero;
    private static readonly TimeSpan ShortCorrectionExecuteDelay = TimeSpan.Zero;
    private static readonly TimeSpan LoadCorrectionExecuteDelay = TimeSpan.FromMilliseconds(2000);
    private static readonly TimeSpan LoadCorrectionEnableDelay = TimeSpan.FromMilliseconds(500);

    public CorrectionViewModel(
        IRegionManager regionManager,
        ICorrectionParameterProvider correctionParameterProvider,
        MachineBase machine,
        IMachineDeviceContext devices,
        IAppNotificationService notificationService,
        ILocalizationService localizationService,
        MesConnectionStatus mesConnectionStatus)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        this.correctionParameterProvider = correctionParameterProvider ?? throw new ArgumentNullException(nameof(correctionParameterProvider));
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.mesConnectionStatus = mesConnectionStatus ?? throw new ArgumentNullException(nameof(mesConnectionStatus));
        this.mesConnectionStatus.PropertyChanged += OnMesConnectionStatusChanged;
        this.correctionParameterProvider.ParametersChanged += OnCorrectionParametersChanged;
        LoadCorrectionInstrumentOptions();
        RefreshCorrectionParametersFromStandardSample();
    }

    public AsyncDelegateCommand EnsureDefaultContentCommand => ensureDefaultContentCommand ??= new AsyncDelegateCommand(EnsureDefaultContentNavigatedAsync);

    public AsyncDelegateCommand ExecuteOpenCorrectionCommand => executeOpenCorrectionCommand ??= new AsyncDelegateCommand(ExecuteOpenCorrectionAsync, CanExecuteCorrection);

    public AsyncDelegateCommand ExecuteShortCorrectionCommand => executeShortCorrectionCommand ??= new AsyncDelegateCommand(ExecuteShortCorrectionAsync, CanExecuteCorrection);

    public AsyncDelegateCommand ExecuteLoadCorrectionCommand => executeLoadCorrectionCommand ??= new AsyncDelegateCommand(ExecuteLoadCorrectionAsync, CanExecuteCorrection);

    public IReadOnlyList<string> LoadTypeItems
    {
        get => loadTypeItems;
        private set => SetProperty(ref loadTypeItems, value ?? []);
    }

    public string LoadType
    {
        get => loadType;
        set
        {
            if (SetProperty(ref loadType, value ?? string.Empty))
            {
                RefreshCorrectionParametersFromStandardSample();
            }
        }
    }

    public string DcLoad
    {
        get => dcLoad;
        set => SetProperty(ref dcLoad, value ?? string.Empty);
    }

    public string Frequency
    {
        get => frequency;
        set => SetProperty(ref frequency, value ?? string.Empty);
    }

    public string FrequencyUnit
    {
        get => frequencyUnit;
        private set => SetProperty(ref frequencyUnit, value ?? string.Empty);
    }

    public string Voltage
    {
        get => voltage;
        set => SetProperty(ref voltage, string.IsNullOrWhiteSpace(value) ? "1" : value);
    }

    public string VoltageUnit
    {
        get => voltageUnit;
        private set => SetProperty(ref voltageUnit, value ?? string.Empty);
    }

    public string LsStandardValue
    {
        get => lsStandardValue;
        private set => SetProperty(ref lsStandardValue, value ?? string.Empty);
    }

    public string LsStandardUnit
    {
        get => lsStandardUnit;
        private set => SetProperty(ref lsStandardUnit, value ?? string.Empty);
    }

    public string RsStandardValue
    {
        get => rsStandardValue;
        private set => SetProperty(ref rsStandardValue, value ?? string.Empty);
    }

    public string RsStandardUnit
    {
        get => rsStandardUnit;
        private set => SetProperty(ref rsStandardUnit, value ?? string.Empty);
    }

    public string LsCorrectionValue
    {
        get => lsCorrectionValue;
        set => SetProperty(ref lsCorrectionValue, value ?? string.Empty);
    }

    public string RsCorrectionValue
    {
        get => rsCorrectionValue;
        set => SetProperty(ref rsCorrectionValue, value ?? string.Empty);
    }

    public bool CanEditCorrectionParameters => mesConnectionStatus.State != MesConnectionState.Online;

    public bool AreCorrectionParametersReadOnly => !CanEditCorrectionParameters;

    public bool IsCorrectionBusy
    {
        get => isCorrectionBusy;
        private set
        {
            if (SetProperty(ref isCorrectionBusy, value))
            {
                executeOpenCorrectionCommand?.RaiseCanExecuteChanged();
                executeShortCorrectionCommand?.RaiseCanExecuteChanged();
                executeLoadCorrectionCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task EnsureDefaultContentNavigatedAsync()
    {
        if (defaultContentNavigated || isNavigatingDefaultContent)
        {
            return;
        }

        isNavigatingDefaultContent = true;
        try
        {
            NavigationResult result = await regionManager.RequestNavigateAsync(RegionNames.CorrectionRegion, ViewNames.StandardView);
            defaultContentNavigated = result.Result;
        }
        finally
        {
            isNavigatingDefaultContent = false;
        }
    }

    private bool CanExecuteCorrection()
        => !IsCorrectionBusy && FindCorrectionInstrument() != null;


    private async Task ExecuteOpenCorrectionAsync()
    {
        await ExecuteCorrectionActionAsync(
            localizationService.T("Correction.Action.Open", "开路"),
            async instrument =>
            {
                await instrument.ExecuteOpenCorrectionAsync(CreateCorrectionConditionRequest()).ConfigureAwait(false);
                await Task.Delay(OpenCorrectionExecuteDelay).ConfigureAwait(false);
                return await ReadCorrectionDataWithRetryAsync(instrument.ReadOpenCorrectionAsync).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private async Task ExecuteShortCorrectionAsync()
    {
        await ExecuteCorrectionActionAsync(
            localizationService.T("Correction.Action.Short", "短路"),
            async instrument =>
            {
                await instrument.ExecuteShortCorrectionAsync(CreateCorrectionConditionRequest()).ConfigureAwait(false);
                await Task.Delay(ShortCorrectionExecuteDelay).ConfigureAwait(false);
                return await ReadCorrectionDataWithRetryAsync(instrument.ReadShortCorrectionAsync).ConfigureAwait(false);
            }).ConfigureAwait(false);
    }

    private async Task ExecuteLoadCorrectionAsync()
    {
        await ExecuteCorrectionActionAsync(
            localizationService.T("Correction.Action.Correct", "校正"),
            async instrument =>
            {
                try
                {
                    InstrumentLoadCorrectionRequest request = CreateLoadCorrectionRequest();
                    await instrument.ExecuteLoadCorrectionAsync(request).ConfigureAwait(false);
                    await Task.Delay(LoadCorrectionExecuteDelay).ConfigureAwait(false);
                    await instrument.EnableLoadCorrectionAsync().ConfigureAwait(false);
                    await Task.Delay(LoadCorrectionEnableDelay).ConfigureAwait(false);
                    return await ReadCorrectionDataWithRetryAsync(instrument.ReadLoadCorrectionAsync).ConfigureAwait(false);
                }
                finally
                {
                    // Hioki load correction loads a panel first. Re-apply the current production
                    // configuration even after a correction failure so Panel 1 cannot leave the
                    // instrument with stale/zero Ls and Rs comparator limits.
                    if (instrument is IConfigurableDevice configurableDevice)
                    {
                        await configurableDevice.ApplyConfigAsync().ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
    }

    private async Task ExecuteCorrectionActionAsync(
        string actionName,
        Func<IInstrumentCorrection, Task<InstrumentCorrectionData>> action)
    {
        IInstrumentCorrection? instrument = FindCorrectionInstrument();
        if (instrument == null)
        {
            await notificationService.WarningAsync(localizationService.T("Correction.Message.NoInstrument", "未找到支持校正的仪表。"), localizationService.T("Correction.Header.Correction", "校正")).ConfigureAwait(true);
            return;
        }

        IsCorrectionBusy = true;
        try
        {
            InstrumentCorrectionData data = await action(instrument).ConfigureAwait(true);
            ApplyCorrectionData(data);
            await notificationService.InfoAsync(localizationService.TF("Correction.Message.ActionCompleted", "{0}已完成。", actionName), localizationService.T("Correction.Header.Correction", "校正")).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await notificationService.ErrorAsync(localizationService.TF("Correction.Message.ActionFailed", "{0}失败：{1}", actionName, ex.Message), localizationService.T("Correction.Header.Correction", "校正")).ConfigureAwait(true);
        }
        finally
        {
            IsCorrectionBusy = false;
        }
    }

    private static async Task<InstrumentCorrectionData> ReadCorrectionDataWithRetryAsync(Func<CancellationToken, ValueTask<InstrumentCorrectionData>> readAsync)
    {
        using var timeoutCts = new CancellationTokenSource(CorrectionReadTimeout);
        Exception? lastException = null;

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                return await readAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                try
                {
                    await Task.Delay(CorrectionReadRetryInterval, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        throw lastException ?? new TimeoutException("Instrument correction result read timed out.");
    }
    private InstrumentCorrectionConditionRequest CreateCorrectionConditionRequest()
    {
        string frequencyUnit = FrequencyUnit;
        string voltageUnit = VoltageUnit;
        return new InstrumentCorrectionConditionRequest(
            TryParseNullableDouble(Frequency),
            string.IsNullOrWhiteSpace(frequencyUnit) ? null : frequencyUnit,
            TryParseNullableDouble(Voltage),
            string.IsNullOrWhiteSpace(voltageUnit) ? null : voltageUnit);
    }
    private InstrumentLoadCorrectionRequest CreateLoadCorrectionRequest()
    {
        if (string.IsNullOrWhiteSpace(LsStandardValue))
        {
            throw new InvalidOperationException(localizationService.T("Correction.Message.MissingLsStandardValue", "标准件中没有 LS 中心值，无法执行校正。"));
        }

        if (string.IsNullOrWhiteSpace(RsStandardValue))
        {
            throw new InvalidOperationException(localizationService.T("Correction.Message.MissingRsStandardValue", "标准件中没有 RS 中心值，无法执行校正。"));
        }

        double lsValue = ConvertInductanceToHenry(ParseRequiredDouble(LsStandardValue, "LS", localizationService.T("Correction.Field.StandardValue", "中心值")), LsStandardUnit);
        double rsValue = ConvertResistanceToOhm(ParseRequiredDouble(RsStandardValue, "RS", localizationService.T("Correction.Field.StandardValue", "中心值")), RsStandardUnit);

        string frequencyUnit = FrequencyUnit;
        string voltageUnit = VoltageUnit;
        double? frequency = TryParseNullableDouble(Frequency);
        double? voltage = TryParseNullableDouble(Voltage);

        return new InstrumentLoadCorrectionRequest(
            lsValue,
            rsValue,
            LoadType,
            frequency,
            string.IsNullOrWhiteSpace(frequencyUnit) ? null : frequencyUnit,
            voltage,
            string.IsNullOrWhiteSpace(voltageUnit) ? null : voltageUnit);
    }

    private void ApplyCorrectionData(InstrumentCorrectionData data)
    {
        LsCorrectionValue = FormatCorrectionValue(data.PrimaryValue);
        RsCorrectionValue = FormatCorrectionValue(data.SecondaryValue);
    }

    private static string FormatCorrectionValue(double value)
        => value.ToString("G10", CultureInfo.InvariantCulture);

    private double ParseRequiredDouble(string? value, string parameterName, string valueName)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
        {
            return result;
        }

        throw new InvalidOperationException(localizationService.TF("Correction.Message.InvalidNumber", "{0}{1}不是有效数字：{2}", parameterName, valueName, value ?? string.Empty));
    }

    private static double? TryParseNullableDouble(string? value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
        {
            return result;
        }

        return null;
    }

    private static double ConvertInductanceToHenry(double value, string? unit)
    {
        string normalizedUnit = NormalizeUnit(unit);
        return normalizedUnit switch
        {
            "H" => value,
            "MH" => value / 1_000D,
            "UH" => value / 1_000_000D,
            "NH" => value / 1_000_000_000D,
            _ => value
        };
    }

    private static double ConvertResistanceToOhm(double value, string? unit)
    {
        string normalizedUnit = NormalizeUnit(unit);
        return normalizedUnit switch
        {
            "OHM" => value,
            "MOHM" => value / 1_000D,
            "UOHM" => value / 1_000_000D,
            "NOHM" => value / 1_000_000_000D,
            _ => value
        };
    }

    private static string NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return string.Empty;
        }

        return unit.Trim()
            .Replace("\u03A9", "OHM", StringComparison.OrdinalIgnoreCase)
            .Replace("\u2126", "OHM", StringComparison.OrdinalIgnoreCase)
            .Replace("\u60DF", "OHM", StringComparison.OrdinalIgnoreCase)
            .Replace("\u03BC", "U", StringComparison.OrdinalIgnoreCase)
            .Replace("\u00B5", "U", StringComparison.OrdinalIgnoreCase)
            .Replace("\u6E2D", "U", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();
    }
    protected override void Dispose(bool disposing)
    {
        if (!disposing || disposed)
        {
            base.Dispose(disposing);
            return;
        }

        disposed = true;
        correctionParameterProvider.ParametersChanged -= OnCorrectionParametersChanged;
        mesConnectionStatus.PropertyChanged -= OnMesConnectionStatusChanged;
        base.Dispose(disposing);
    }

    private void OnMesConnectionStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MesConnectionStatus.State))
        {
            RaisePropertyChanged(nameof(CanEditCorrectionParameters));
            RaisePropertyChanged(nameof(AreCorrectionParametersReadOnly));
        }
    }
    private void OnCorrectionParametersChanged(object? sender, EventArgs e)
        => RefreshCorrectionParametersFromStandardSample();

    private void LoadCorrectionInstrumentOptions()
    {
        IInstrumentCorrection? instrument = FindCorrectionInstrument();
        IReadOnlyList<string> supportedLoadTypes = instrument?.SupportedLoadCorrectionTypes ?? [];
        LoadTypeItems = supportedLoadTypes;

        string defaultLoadType = instrument?.DefaultLoadCorrectionType ?? string.Empty;
        if (string.IsNullOrWhiteSpace(defaultLoadType) && supportedLoadTypes.Count > 0)
        {
            defaultLoadType = supportedLoadTypes[0];
        }

        LoadType = defaultLoadType;
    }

    private IInstrumentCorrection? FindCorrectionInstrument()
    {
        foreach (TestStationModel station in machine.TestStations.Where(HasStationCalibrationOperation))
        {
            foreach (string deviceId in station.InstrumentDeviceIds.Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                if (devices.TryGet(deviceId, out IInstrumentCorrection? instrument) && instrument != null)
                {
                    return instrument;
                }
            }
        }

        return null;
    }

    private static bool HasStationCalibrationOperation(TestStationModel station)
        => station.Operations.Any(static operation => string.Equals(
            operation.Code,
            StationOperationDescriptor.Calibration,
            StringComparison.OrdinalIgnoreCase));

    private void RefreshCorrectionParametersFromStandardSample()
    {
        CorrectionParameterSnapshot snapshot = correctionParameterProvider.CreateSnapshot(GetCorrectionInstrumentConfig());
        LsStandardValue = snapshot.LsStandardValue;
        LsStandardUnit = snapshot.LsStandardUnit;
        RsStandardValue = snapshot.RsStandardValue;
        RsStandardUnit = snapshot.RsStandardUnit;
        Frequency = snapshot.Frequency;
        FrequencyUnit = snapshot.FrequencyUnit;
        Voltage = snapshot.Voltage;
        VoltageUnit = snapshot.VoltageUnit;
    }

    private object? GetCorrectionInstrumentConfig()
        => FindCorrectionInstrument()?.DeviceParameter;

}




