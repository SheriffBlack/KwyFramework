using System.Globalization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Kwy.Device.Abstractions.Instrument;
using KwyTemplate.App.Orchestration;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using System.Windows;
using Kwy.MVVM.Core;
using Kwy.MVVM.Messaging;
using Kwy.MVVM.Regions;
using KwyTemplate.App.Models;
using KwyTemplate.App.Messages;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Navigation;
using KwyTemplate.Flow.DataDeals;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;

namespace KwyTemplate.App.ViewModels;

public class CompensateViewModel : BindableBase, INavigationAware
{
    private const string StandardFlowCode = "Standard";
    private const string ConfirmFlowCode = "Confirm";
    private const string PolarityForwardFlowCode = "PolarityForward";
    private const string PolarityReverseFlowCode = "PolarityReverse";
    private const double LimitComparisonTolerance = 1e-8;
    private readonly IRegionManager regionManager;
    private readonly MachineBase machine;
    private readonly StandardSampleState sampleState;
    private readonly IAppNotificationService notificationService;
    private readonly IMesStandardSampleService mesStandardSampleService;
    private readonly IMessageBus messageBus;
    private readonly IProductionContext productionContext;
    private readonly ILocalizationService localizationService;
    private readonly IDisposable productionContextClearedSubscription;
    private AsyncDelegateCommand? executeCheckCommand;
    private AsyncDelegateCommand? ensureDefaultContentCommand;
    private bool defaultContentNavigated;
    private bool isNavigatingDefaultContent;
    private bool isChecking;
    private readonly CompensateStationMonitor stationMonitor;
    private readonly SemaphoreSlim measurementStateGate = new(1, 1);

    public CompensateViewModel(
        IRegionManager regionManager,
        MachineBase machine,
        StandardSampleState sampleState,
        IAppNotificationService notificationService,
        IMesStandardSampleService mesStandardSampleService,
        IProductionContext productionContext,
        IMessageBus messageBus,
        ILocalizationService localizationService)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.mesStandardSampleService = mesStandardSampleService ?? throw new ArgumentNullException(nameof(mesStandardSampleService));
        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
        this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.localizationService.LanguageChanged += OnLanguageChanged;
        productionContextClearedSubscription = this.messageBus.Subscribe<CompensateViewModel, ProductionContextClearedMessage>(
            this,
            static (viewModel, _) => viewModel.ClearProductionContextState());
        this.sampleState.StandardSample.LimitItems.CollectionChanged += OnSampleLimitItemsChanged;
        this.sampleState.ConfirmSample.LimitItems.CollectionChanged += OnSampleLimitItemsChanged;
        stationMonitor = new CompensateStationMonitor(machine);
        LoadCheckFlowItems();
        LoadCheckItems();
        LoadPolarityCheckItems();
    }

    public ObservableCollection<CheckFlowItemModel> CheckFlowItems { get; } = [];

    public ObservableCollection<StationCheckItemModel> CheckItems { get; } = [];

    public ObservableCollection<PolarityCheckItemModel> PolarityCheckItems { get; } = [];

    public Visibility PolarityCheckVisibility => HasPolarityCheckStation() ? Visibility.Visible : Visibility.Collapsed;

    // These are nullable by design: machines without polarity stations do not create these flows.
    public CheckFlowItemModel? PolarityForwardCheckFlow
        => CheckFlowItems.FirstOrDefault(item => string.Equals(item.Code, PolarityForwardFlowCode, StringComparison.OrdinalIgnoreCase));

    public CheckFlowItemModel? PolarityReverseCheckFlow
        => CheckFlowItems.FirstOrDefault(item => string.Equals(item.Code, PolarityReverseFlowCode, StringComparison.OrdinalIgnoreCase));

    public AsyncDelegateCommand ExecuteCheckCommand => executeCheckCommand ??= new AsyncDelegateCommand(ExecuteCheckAsync, CanExecuteCheck);

    public AsyncDelegateCommand EnsureDefaultContentCommand => ensureDefaultContentCommand ??= new AsyncDelegateCommand(EnsureDefaultContentNavigatedAsync);

    public bool IsChecking
    {
        get => isChecking;
        private set
        {
            if (SetProperty(ref isChecking, value))
            {
                executeCheckCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        stationMonitor.Stop();
        _ = SetCheckViewActiveAsync(false);
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        LoadCheckFlowItems();
        LoadCheckItems();
        LoadPolarityCheckItems();
        RaisePropertyChanged(nameof(PolarityCheckVisibility));
        ResetCheckFlowItems();
        ApplySampleLimitReferences();
        _ = EnterCheckViewAsync();
        stationMonitor.Start(HandleManualStationMeasurementAsync);
    }

    private async Task EnterCheckViewAsync()
    {
        await SetCheckViewActiveAsync(true).ConfigureAwait(false);
    }

    private async Task SetCheckViewActiveAsync(bool isActive)
    {
        try
        {
            await machine.SetCheckViewActiveAsync(isActive, DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Do not block navigation when PLC is offline; the connection status will surface elsewhere.
        }
    }

    public async Task EnsureDefaultContentNavigatedAsync()
    {
        if (defaultContentNavigated || isNavigatingDefaultContent || HasStationCalibrationOperation())
        {
            return;
        }

        isNavigatingDefaultContent = true;
        try
        {
            NavigationResult result = await regionManager.RequestNavigateAsync(RegionNames.CompensateRegion, ViewNames.StandardView);
            defaultContentNavigated = result.Result;
        }
        finally
        {
            isNavigatingDefaultContent = false;
        }
    }

    private void LoadCheckFlowItems()
    {
        CheckFlowItems.Clear();
        CheckFlowItems.Add(new CheckFlowItemModel(StandardFlowCode, localizationService.T("Compensate.Flow.Standard", "标准件")));
        CheckFlowItems.Add(new CheckFlowItemModel(ConfirmFlowCode, localizationService.T("Compensate.Flow.Confirm", "确认件")));

        if (HasPolarityCheckStation())
        {
            CheckFlowItems.Add(new CheckFlowItemModel(PolarityForwardFlowCode, localizationService.T("Compensate.Flow.PolarityForward", "极性正向件")));
            CheckFlowItems.Add(new CheckFlowItemModel(PolarityReverseFlowCode, localizationService.T("Compensate.Flow.PolarityReverse", "极性反向件")));
        }

        RaisePropertyChanged(nameof(PolarityForwardCheckFlow));
        RaisePropertyChanged(nameof(PolarityReverseCheckFlow));
    }

    private void ResetCheckFlowItems()
    {

        foreach (CheckFlowItemModel item in CheckFlowItems)
        {
            item.IsCompleted = false;
            item.ResetResult();
        }

        foreach (StationCheckItemModel item in CheckItems)
        {
            item.StandardMeasuredValue = string.Empty;
            item.ConfirmMeasuredValue = string.Empty;
            item.StandardMeasuredValueOutOfRange = false;
            item.ConfirmMeasuredValueOutOfRange = false;
        }

        ResetPolarityMeasurements(PolarityForwardFlowCode);
        ResetPolarityMeasurements(PolarityReverseFlowCode);

        executeCheckCommand?.RaiseCanExecuteChanged();
    }

    private void ClearProductionContextState()
    {
        ResetCheckFlowItems();
        ApplySampleLimitReferences();
    }
    private void LoadPolarityCheckItems()
    {
        PolarityCheckItems.Clear();
        foreach (TestStationModel station in machine.TestStations.Where(station => HasStationCheckOperation(station) && HasZThetaMeasurement(station)))
        {
            PolarityCheckItems.Add(new PolarityCheckItemModel(station, localizationService));
        }
    }

    private void LoadCheckItems()
    {
        CheckItems.Clear();

        foreach (TestStationModel station in machine.TestStations.Where(static station => station.ShowInResultGrid).Where(HasStationCheckOperation))
        {
            foreach (string testName in GetStationCheckItemNames(station))
            {
                CheckItems.Add(new StationCheckItemModel(station, testName));
            }
        }

        ApplySampleLimitReferences();
        executeCheckCommand?.RaiseCanExecuteChanged();
    }
    private static IEnumerable<string> GetStationCheckItemNames(TestStationModel station)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string testName in station.OrderedTestNames)
        {
            foreach (string itemName in SplitTestNames(testName))
            {
                if (seen.Add(itemName))
                {
                    yield return itemName;
                }
            }
        }

        foreach (IStationInstrumentOperation operation in station.StationDataDeals.OfType<IStationInstrumentOperation>())
        {
            foreach (string itemName in SplitTestNames(operation.TestName))
            {
                if (seen.Add(itemName))
                {
                    yield return itemName;
                }
            }
        }
    }

    private static IEnumerable<string> SplitTestNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string item in value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                yield return item;
            }
        }
    }
    /// <summary>
    /// Gets the current check step. Automatic and manual supplement tests share this step so measurements land in the correct area.
    /// Standard sample and confirm sample are processed in order by the first unfinished item.
    /// </summary>
    private CheckFlowItemModel? GetCurrentCheckStep()
        => CheckFlowItems.FirstOrDefault(item => !item.IsCompleted);
    private async Task ExecuteCheckAsync()
    {
        CheckFlowItemModel? currentStep = GetCurrentCheckStep();
        if (currentStep == null)
        {
            return;
        }

        IsChecking = true;
        try
        {
            var progress = new Progress<MachineExamineMeasurement>(measurement => _ = ApplyAutomaticMeasurementAsync(currentStep.Code, measurement));
            MachineExamineResult result = currentStep.Code switch
            {
                StandardFlowCode => await machine.ExecuteExamineStandardAsync(progress),
                ConfirmFlowCode => await machine.ExecuteExamineConfirmAsync(progress),
                PolarityForwardFlowCode => await machine.ExecuteExamineAsync(PolarityForwardFlowCode, progress),
                PolarityReverseFlowCode => await machine.ExecuteExamineAsync(PolarityReverseFlowCode, progress),
                _ => MachineExamineResult.Failed()
            };

            await measurementStateGate.WaitAsync(DestroyToken).ConfigureAwait(false);
            try
            {
                await InvokeOnUiAsync(async () =>
                {
                    // Progress callbacks provide live updates; the completed flow result is the authoritative snapshot.
                    if (IsPolarityFlow(currentStep.Code))
                    {
                        ResetPolarityMeasurements(currentStep.Code);
                    }
                    ApplyExamineMeasurements(currentStep.Code, result);
                    await RefreshStepResultAsync(currentStep, markFailedWhenIncomplete: !result.IsCompleted).ConfigureAwait(true);
                }).ConfigureAwait(false);
            }
            finally
            {
                measurementStateGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await notificationService.ErrorAsync(string.Format(CultureInfo.CurrentCulture, localizationService.T("Compensate.Message.ExecuteFailed", "点检执行失败：\\n{0}").Replace("\\n", Environment.NewLine), ex.Message), localizationService.T("Compensate.Title.CheckFailed", "点检失败")).ConfigureAwait(true);
        }
        finally
        {
            IsChecking = false;
            executeCheckCommand?.RaiseCanExecuteChanged();
        }
    }

    private async Task ApplyAutomaticMeasurementAsync(string flowCode, MachineExamineMeasurement measurement)
    {
        await measurementStateGate.WaitAsync(DestroyToken).ConfigureAwait(false);
        try
        {
            await InvokeOnUiAsync(() =>
            {
                // The instrument may have already pushed values through progress; only fill the missing station value here.
                {
                    ApplyExamineMeasurement(flowCode, measurement);
                }

                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }
        finally
        {
            measurementStateGate.Release();
        }
    }
    private async Task HandleManualStationMeasurementAsync(CompensateStationMeasurement sample, CancellationToken cancellationToken)
    {
        await measurementStateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var measurement = new MachineExamineMeasurement(
                sample.Station.StationId,
                sample.Station.StationName,
                sample.TestName,
                sample.Measurement);

            await InvokeOnUiAsync(async () =>
            {
                CheckFlowItemModel? currentStep = GetCurrentCheckStep();
                if (currentStep == null)
                {
                    return;
                }

                // Manual supplement test is posted to the current unfinished check step.
                bool inRange = ApplyExamineMeasurement(currentStep.Code, measurement);
                await RefreshStepResultAsync(
                    currentStep,
                    markFailedWhenIncomplete: !inRange,
                    showFailureNotification: false).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }
        finally
        {
            measurementStateGate.Release();
        }
    }

    private bool HasMeasurement(string flowCode, int stationId, string testName)
    {
        StationCheckItemModel? item = FindCheckItem(stationId, testName);
        if (item == null)
        {
            return false;
        }

        if (string.Equals(flowCode, StandardFlowCode, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(item.StandardMeasuredValue);
        }

        if (string.Equals(flowCode, ConfirmFlowCode, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(item.ConfirmMeasuredValue);
        }

        return false;
    }
    private async Task RefreshStepResultAsync(
        CheckFlowItemModel currentStep,
        bool markFailedWhenIncomplete,
        bool showFailureNotification = true)
    {
        bool hasAllMeasurements = HasAllMeasurements(currentStep.Code);
        bool hasOutOfRange = HasOutOfRangeMeasurement(currentStep.Code);

        if (hasAllMeasurements && !hasOutOfRange)
        {
            currentStep.IsCompleted = true;
            currentStep.SetResult(true);

            if (IsAllCheckFlowCompleted() && !await SaveCompletedCheckResultsAsync().ConfigureAwait(true))
            {
                currentStep.IsCompleted = false;
                currentStep.SetResult(false);
                executeCheckCommand?.RaiseCanExecuteChanged();
                return;
            }

            await ShowExamineResultAsync(currentStep, true).ConfigureAwait(true);
            executeCheckCommand?.RaiseCanExecuteChanged();
            return;
        }

        if (hasOutOfRange || markFailedWhenIncomplete)
        {
            currentStep.IsCompleted = false;
            currentStep.SetResult(false);
            if (showFailureNotification)
            {
                await ShowExamineResultAsync(currentStep, false).ConfigureAwait(true);
            }
            executeCheckCommand?.RaiseCanExecuteChanged();
        }
    }
    private async Task<bool> SaveCompletedCheckResultsAsync()
    {
        string title = localizationService.T("Compensate.Title.CheckSave", "点检保存");
        if (string.IsNullOrWhiteSpace(productionContext.EquipmentNo))
        {
            await notificationService.WarningAsync(localizationService.T("Compensate.Message.EquipmentNoRequired", "机台号为空，无法保存点检数据。"), title).ConfigureAwait(true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(productionContext.WorkOrderNo))
        {
            await notificationService.WarningAsync(localizationService.T("Compensate.Message.WorkOrderRequired", "工单为空，无法保存点检数据。"), title).ConfigureAwait(true);
            return false;
        }

        List<MesMeasurementResult> measurements = [];
        if (!AppendCheckSaveMeasurements(StandardFlowCode, sampleState.StandardSample, measurements))
        {
            await notificationService.WarningAsync(localizationService.T("Compensate.Message.StandardDataRequired", "标准件编号为空或没有可保存的标准件点检数据。"), title).ConfigureAwait(true);
            return false;
        }

        if (!AppendCheckSaveMeasurements(ConfirmFlowCode, sampleState.ConfirmSample, measurements))
        {
            await notificationService.WarningAsync(localizationService.T("Compensate.Message.ConfirmDataRequired", "确认件编号为空或没有可保存的确认件点检数据。"), title).ConfigureAwait(true);
            return false;
        }

        DateTimeOffset completedAt = DateTimeOffset.Now;
        var request = new MesStandardSampleCheckSaveRequest(
            Context: CreateMesContext(),
            WorkOrderNo: productionContext.WorkOrderNo,
            SampleCode: sampleState.StandardSample.SampleCode.Trim(),
            Passed: measurements.All(static item => item.Passed),
            Time: completedAt,
            Measurements: measurements);

        MesResult equipmentSaveResult = await mesStandardSampleService.SaveStandardSampleCheckEquipmentAsync(request).ConfigureAwait(true);
        if (!equipmentSaveResult.IsSuccess)
        {
            await notificationService.ErrorAsync(localizationService.TF("Compensate.Message.LocalSaveFailed", "点检数据本地保存失败：{0}", equipmentSaveResult.Message), localizationService.T("Compensate.Title.CheckSaveFailed", "点检保存失败")).ConfigureAwait(true);
            return false;
        }

        MesResult result = await mesStandardSampleService.SaveStandardSampleCheckAsync(request).ConfigureAwait(true);
        if (!IsMesAccepted(result))
        {
            await notificationService.ErrorAsync(MesFailureMessageFormatter.Format(title, result), localizationService.T("Compensate.Title.CheckSaveFailed", "点检保存失败")).ConfigureAwait(true);
            return false;
        }
        await machine.SetCheckCompletedAsync(true, DestroyToken).ConfigureAwait(true);
        await ConfirmCheckStopSignalsCompletedAsync().ConfigureAwait(true);
        return true;
    }
    private async Task ConfirmCheckStopSignalsCompletedAsync()
    {
        if (machine is not IMachinePlcStopSignalMachine stopSignalMachine)
        {
            return;
        }

        try
        {
            await stopSignalMachine.SetCheckStopSignalsCompletedAsync(DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Check save succeeded; PLC acknowledgement failure is logged by the device layer and must not block the check flow.
        }
    }
    private bool IsAllCheckFlowCompleted()
        => CheckFlowItems.Count > 0 && CheckFlowItems.All(static item => item.IsCompleted);
    private bool AppendCheckSaveMeasurements(string flowCode, StandardSamplePanelModel panel, ICollection<MesMeasurementResult> results)
    {
        if (string.IsNullOrWhiteSpace(panel.SampleCode))
        {
            return false;
        }

        int beforeCount = results.Count;
        foreach (MesMeasurementResult measurement in CreateCheckSaveMeasurements(flowCode, panel.SampleCode.Trim()))
        {
            results.Add(measurement);
        }

        return results.Count > beforeCount;
    }
    private IReadOnlyList<MesMeasurementResult> CreateCheckSaveMeasurements(string flowCode, string sampleCode)
    {
        var results = new List<MesMeasurementResult>();
        foreach (StationCheckItemModel item in CheckItems)
        {
            bool isStandard = string.Equals(flowCode, StandardFlowCode, StringComparison.OrdinalIgnoreCase);
            string valueText = isStandard ? item.StandardMeasuredValue : item.ConfirmMeasuredValue;
            if (!double.TryParse(valueText, out double value))
            {
                continue;
            }

            string lowerText = isStandard ? item.StandardLowerLimit : item.ConfirmLowerLimit;
            string upperText = isStandard ? item.StandardUpperLimit : item.ConfirmUpperLimit;
            string standardText = isStandard ? item.StandardValue : item.ConfirmValue;
            bool outOfRange = isStandard ? item.StandardMeasuredValueOutOfRange : item.ConfirmMeasuredValueOutOfRange;

            results.Add(new MesMeasurementResult(
                ParameterId: isStandard ? (EmptyToNull(item.StandardMeterType) ?? item.DisplayName) : (EmptyToNull(item.ConfirmMeterType) ?? item.DisplayName),
                DisplayName: item.DisplayName,
                Value: value,
                Passed: !outOfRange,
                LowerLimit: TryParseNullableDouble(lowerText),
                UpperLimit: TryParseNullableDouble(upperText),
                StandardValue: TryParseNullableDouble(standardText),
                Unit: isStandard ? EmptyToNull(item.StandardUnit) : EmptyToNull(item.ConfirmUnit),
                SampleId: sampleCode,
                MeterType: isStandard ? EmptyToNull(item.StandardMeterType) : EmptyToNull(item.ConfirmMeterType),
                MeterSerialNo: isStandard ? EmptyToNull(item.StandardSerialNo) : EmptyToNull(item.ConfirmSerialNo),
                ItemName: isStandard ? (EmptyToNull(item.StandardItemName) ?? item.DisplayName) : (EmptyToNull(item.ConfirmItemName) ?? item.DisplayName),
                Frequency: isStandard ? EmptyToNull(item.StandardFrequency) : EmptyToNull(item.ConfirmFrequency),
                FrequencyUnit: isStandard ? EmptyToNull(item.StandardFrequencyUnit) : EmptyToNull(item.ConfirmFrequencyUnit)));
        }

        return results;
    }

    private MesRequestContext CreateMesContext()
        => new(
            MachineId: productionContext.EquipmentNo,
            MachineName: machine.MachineName,
            OperatorId: productionContext.OperatorNo,
            WorkOrderNo: productionContext.WorkOrderNo);

    private static bool IsMesAccepted(MesResult result)
        => result.Exchange?.ReturnCode is int returnCode ? returnCode == 0 : result.IsSuccess;

    private static double? TryParseNullableDouble(string value)
        => TryParseDouble(value, out double result) ? result : null;

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private bool HasAllMeasurements(string flowCode)
    {
        if (string.Equals(flowCode, StandardFlowCode, StringComparison.OrdinalIgnoreCase))
        {
            return CheckItems.Count > 0 && CheckItems.All(item => !string.IsNullOrWhiteSpace(item.StandardMeasuredValue));
        }

        if (string.Equals(flowCode, ConfirmFlowCode, StringComparison.OrdinalIgnoreCase))
        {
            return CheckItems.Count > 0 && CheckItems.All(item => !string.IsNullOrWhiteSpace(item.ConfirmMeasuredValue));
        }

        if (IsPolarityFlow(flowCode))
        {
            return PolarityCheckItems.Count > 0 && PolarityCheckItems.All(item =>
                GetPolarityValues(item, flowCode).All(static value => !string.IsNullOrWhiteSpace(value)));
        }

        return false;
    }

    private bool HasOutOfRangeMeasurement(string flowCode)
    {
        if (string.Equals(flowCode, StandardFlowCode, StringComparison.OrdinalIgnoreCase))
        {
            return CheckItems.Any(item => item.StandardMeasuredValueOutOfRange);
        }

        if (string.Equals(flowCode, ConfirmFlowCode, StringComparison.OrdinalIgnoreCase))
        {
            return CheckItems.Any(item => item.ConfirmMeasuredValueOutOfRange);
        }

        if (IsPolarityFlow(flowCode))
        {
            bool requirePositive = string.Equals(flowCode, PolarityForwardFlowCode, StringComparison.OrdinalIgnoreCase);
            return PolarityCheckItems
                .SelectMany(item => GetPolarityValues(item, flowCode))
                .Any(value => !TryParseDouble(value, out double parsed) || (requirePositive ? parsed <= 0 : parsed >= 0));
        }

        return true;
    }
    private bool ApplyExamineMeasurements(string flowCode, MachineExamineResult result)
    {
        bool allInRange = result.IsCompleted;

        foreach (MachineExamineMeasurement measurement in result.Measurements)
        {
            allInRange &= ApplyExamineMeasurement(flowCode, measurement);
        }

        return allInRange;
    }

    private bool ApplyExamineMeasurement(string flowCode, MachineExamineMeasurement measurement, bool overwriteExisting = true)
    {
        if (IsPolarityFlow(flowCode))
        {
            return ApplyPolarityMeasurement(flowCode, measurement);
        }

        bool anyApplied = false;
        bool allInRange = true;

        foreach ((StationCheckItemModel item, InstrumentMeasurementValue measurementValue) in EnumerateMeasurementItems(measurement))
        {
            if (!overwriteExisting && HasMeasurement(flowCode, item.StationId, item.TestName))
            {
                continue;
            }

            if (string.Equals(flowCode, StandardFlowCode, StringComparison.OrdinalIgnoreCase))
            {
                double measuredValue = MeasurementUnitConverter.Convert(
                    measurementValue.Value,
                    item.TestName,
                    measurementValue.Unit,
                    item.StandardUnit);
                string valueText = measuredValue.ToString("F4");
                item.StandardMeasuredValue = valueText;
                bool inRange = IsValueInRange(measuredValue, item.StandardLowerLimit, item.StandardUpperLimit);
                item.StandardMeasuredValueOutOfRange = !inRange;
                allInRange &= inRange;
                anyApplied = true;
                continue;
            }

            if (string.Equals(flowCode, ConfirmFlowCode, StringComparison.OrdinalIgnoreCase))
            {
                double measuredValue = MeasurementUnitConverter.Convert(
                    measurementValue.Value,
                    item.TestName,
                    measurementValue.Unit,
                    item.ConfirmUnit);
                string valueText = measuredValue.ToString("F4");
                item.ConfirmMeasuredValue = valueText;
                bool inRange = IsValueInRange(measuredValue, item.ConfirmLowerLimit, item.ConfirmUpperLimit);
                item.ConfirmMeasuredValueOutOfRange = !inRange;
                allInRange &= inRange;
                anyApplied = true;
            }
        }

        return anyApplied && allInRange;
    }

    private bool ApplyPolarityMeasurement(string flowCode, MachineExamineMeasurement measurement)
    {
        PolarityCheckItemModel? item = PolarityCheckItems.FirstOrDefault(candidate => candidate.StationId == measurement.StationId);
        InstrumentMeasurementValue? zValue = measurement.Measurement.Values
            .FirstOrDefault(value => string.Equals(value.Name, "PHASE", StringComparison.OrdinalIgnoreCase))
            ?? measurement.Measurement.Values.FirstOrDefault();
        if (item == null || zValue == null)
        {
            return false;
        }

        ObservableCollection<string> values = GetPolarityValues(item, flowCode);
        int targetIndex = values.IndexOf(string.Empty);
        if (targetIndex < 0)
        {
            return true;
        }

        values[targetIndex] = zValue.Value.ToString("F4", CultureInfo.InvariantCulture);
        bool requirePositive = string.Equals(flowCode, PolarityForwardFlowCode, StringComparison.OrdinalIgnoreCase);
        return requirePositive ? zValue.Value > 0 : zValue.Value < 0;
    }

    private void ResetPolarityMeasurements(string flowCode)
    {
        foreach (PolarityCheckItemModel item in PolarityCheckItems)
        {
            ObservableCollection<string> values = GetPolarityValues(item, flowCode);
            for (int index = 0; index < values.Count; index++)
            {
                values[index] = string.Empty;
            }
        }
    }

    private static ObservableCollection<string> GetPolarityValues(PolarityCheckItemModel item, string flowCode)
        => string.Equals(flowCode, PolarityForwardFlowCode, StringComparison.OrdinalIgnoreCase)
            ? item.ForwardZValues
            : item.ReverseZValues;

    private static bool IsPolarityFlow(string flowCode)
        => string.Equals(flowCode, PolarityForwardFlowCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(flowCode, PolarityReverseFlowCode, StringComparison.OrdinalIgnoreCase);

    private IEnumerable<(StationCheckItemModel Item, InstrumentMeasurementValue Value)> EnumerateMeasurementItems(MachineExamineMeasurement measurement)
    {
        TestStationModel? station = machine.TestStations.FirstOrDefault(item => item.StationId == measurement.StationId);
        if (station == null || measurement.Measurement.Values.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < measurement.Measurement.Values.Count; i++)
        {
            string testName = ResolveMeasurementTestName(station, measurement, i);
            StationCheckItemModel? item = FindCheckItem(measurement.StationId, testName);
            if (item != null)
            {
                yield return (item, measurement.Measurement.Values[i]);
            }
        }
    }

    private static string ResolveMeasurementTestName(TestStationModel station, MachineExamineMeasurement measurement, int valueIndex)
    {
        if (station.OrderedTestNames.Count > valueIndex)
        {
            return station.OrderedTestNames[valueIndex];
        }

        if (valueIndex == 0 && !string.IsNullOrWhiteSpace(measurement.InstrumentCode))
        {
            return measurement.InstrumentCode;
        }

        return measurement.Measurement.Values[valueIndex].Name;
    }

    private StationCheckItemModel? FindCheckItem(int stationId, string testName)
        => CheckItems.FirstOrDefault(checkItem => checkItem.StationId == stationId
            && string.Equals(checkItem.TestName, testName, StringComparison.OrdinalIgnoreCase));
    private async Task ShowExamineResultAsync(CheckFlowItemModel currentStep, bool isInRange)
    {
        string prefix = currentStep.DisplayName;
        if (!isInRange)
        {
            StationCheckItemModel? failedItem = currentStep.Code switch
            {
                StandardFlowCode => CheckItems.FirstOrDefault(item => item.StandardMeasuredValueOutOfRange),
                ConfirmFlowCode => CheckItems.FirstOrDefault(item => item.ConfirmMeasuredValueOutOfRange),
                _ => null
            };

            if (failedItem != null)
            {
                string measuredValue = currentStep.Code == StandardFlowCode
                    ? failedItem.StandardMeasuredValue
                    : failedItem.ConfirmMeasuredValue;
                string lowerLimit = currentStep.Code == StandardFlowCode
                    ? failedItem.StandardLowerLimit
                    : failedItem.ConfirmLowerLimit;
                string upperLimit = currentStep.Code == StandardFlowCode
                    ? failedItem.StandardUpperLimit
                    : failedItem.ConfirmUpperLimit;

                await notificationService.ErrorAsync(
                    string.Format(CultureInfo.CurrentCulture, localizationService.T("Compensate.Message.StepRangeFailed", "{{{0}}}{{{1}}}点检失败！\n{2}未在{3}~{4}范围内").Replace("\\n", Environment.NewLine), prefix, failedItem.DisplayName, measuredValue, lowerLimit, upperLimit),
                    localizationService.T("Compensate.Title.CheckFailed", "点检失败"));
            }
            else
            {
                await notificationService.ErrorAsync(localizationService.TF("Compensate.Message.StepFailed", "{{{0}}}点检失败！", prefix), localizationService.T("Compensate.Title.CheckFailed", "点检失败"));
            }

            return;
        }

        StationCheckItemModel? firstItem = CheckItems.FirstOrDefault();
        string stationName = firstItem?.DisplayName ?? string.Empty;
        await notificationService.SuccessAsync(
            localizationService.TF("Compensate.Message.StepSuccess", "{{{0}}}{{{1}}}点检成功！", prefix, stationName),
            localizationService.T("Compensate.Title.CheckSuccess", "点检成功"));
    }
    private static bool IsValueInRange(double value, string lowerLimitText, string upperLimitText)
    {
        if (!TryParseDouble(lowerLimitText, out double lowerLimit)
            || !TryParseDouble(upperLimitText, out double upperLimit))
        {
            return false;
        }

        // 点检判定使用闭区间：下限 <= 测试值 <= 上限。保留极小容差，避免边界小数误判。
        return value >= lowerLimit - LimitComparisonTolerance
            && value <= upperLimit + LimitComparisonTolerance;
    }

    private static bool TryParseDouble(string value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);

    private void OnSampleLimitItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplySampleLimitReferences();
    }

    private void ApplySampleLimitReferences()
    {
        for (int i = 0; i < CheckItems.Count; i++)
        {
            StationCheckItemModel item = CheckItems[i];
            item.SetStandardLimitItem(GetLimitItem(sampleState.StandardSample.LimitItems, i));
            item.SetConfirmLimitItem(GetLimitItem(sampleState.ConfirmSample.LimitItems, i));
        }
    }

    private static StandardSampleLimitItemModel? GetLimitItem(IList<StandardSampleLimitItemModel> limitItems, int index)
    {
        if (limitItems.Count == 0)
        {
            return null;
        }

        return limitItems[Math.Min(index, limitItems.Count - 1)];
    }


    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        RefreshCheckFlowDisplayNames();
    }

    private void RefreshCheckFlowDisplayNames()
    {
        foreach (PolarityCheckItemModel item in PolarityCheckItems)
        {
            item.RefreshLocalization();
        }

        foreach (CheckFlowItemModel item in CheckFlowItems)
        {
            item.DisplayName = item.Code switch
            {
                StandardFlowCode => localizationService.T("Compensate.Flow.Standard", "标准件"),
                ConfirmFlowCode => localizationService.T("Compensate.Flow.Confirm", "确认件"),
                PolarityForwardFlowCode => localizationService.T("Compensate.Flow.PolarityForward", "极性正向件"),
                PolarityReverseFlowCode => localizationService.T("Compensate.Flow.PolarityReverse", "极性反向件"),
                _ => item.DisplayName
            };
        }
    }

    private bool CanExecuteCheck()
        => !IsChecking && CheckFlowItems.Any(item => !item.IsCompleted);

    private static bool HasStationCheckOperation(TestStationModel station)
        => station.Operations.Any(operation =>
            string.Equals(operation.Code, StationOperationDescriptor.Check, StringComparison.OrdinalIgnoreCase));

    private bool HasPolarityCheckStation()
        => machine.TestStations.Any(station => HasStationCheckOperation(station) && HasZThetaMeasurement(station));

    private static bool HasZThetaMeasurement(TestStationModel station)
    {
        HashSet<string> testNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (string testName in GetStationCheckItemNames(station))
        {
            testNames.Add(testName);
        }

        return testNames.Contains("Z") && testNames.Contains("PHASE");
    }

    private bool HasStationCalibrationOperation()
        => machine.TestStations.Any(station => station.Operations.Any(operation =>
            string.Equals(operation.Code, StationOperationDescriptor.Calibration, StringComparison.OrdinalIgnoreCase)));

    private static Task InvokeOnUiAsync(Func<Task> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return action();
        }

        var completion = new TaskCompletionSource();
        dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await action().ConfigureAwait(true);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
            sampleState.StandardSample.LimitItems.CollectionChanged -= OnSampleLimitItemsChanged;
            productionContextClearedSubscription.Dispose();
            stationMonitor.Stop();
            stationMonitor.Dispose();
            measurementStateGate.Dispose();
            sampleState.ConfirmSample.LimitItems.CollectionChanged -= OnSampleLimitItemsChanged;
        }

        base.Dispose(disposing);
    }
}



