using Kwy.MVVM.Core;
using Kwy.MVVM.Messaging;
using Kwy.UI.DataGrids;
using Kwy.UI.WPF.Components.Logging;
using Kwy.UI.WPF.Components.Toasts;
using Kwy.UI.WPF.Components.Dialogs;
using Kwy.UI.WPF.Controls.Helpers;
using KwyTemplate.App.Input;
using KwyTemplate.App.Messages;
using KwyTemplate.App.Models;
using KwyTemplate.App.Orchestration;
using KwyTemplate.App.Runtime;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Contracts.Security;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using KwyTemplate.Flow.Services;
using KwyTemplate.MES.Abstract.Models;
using KwyTemplate.MES.Abstract.Services;
using KwyTemplate.Security.Licensing;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace KwyTemplate.App.ViewModels;

public sealed class HomeViewModel : BindableBase
{
    private readonly MachineBase machine;
    private readonly IProductionContext productionContext;
    private readonly ICyntecReelScanWorkflow reelScanWorkflow;
    private readonly MesConnectionStatus mesConnectionStatus;
    private readonly IMesConnection mesConnection;
    private readonly IRawInputBarcodeReceiver rawInputBarcodeReceiver;
    private readonly IAppNotificationService notificationService;
    private readonly IToastMessageService toastMessageService;
    private readonly IInputDialogService inputDialogService;
    private readonly ISecurityKeyChecker securityKeyChecker;
    private readonly IPermissionService permissionService;
    private readonly KwyLogService? logService;
    private readonly IMesTrackService mesTrackService;
    private readonly IMesWorkOrderService mesWorkOrderService;
    private readonly BraidOptionsStore braidOptionsStore;
    private readonly MarkPrintOptionsStore markPrintOptionsStore;
    private readonly IProductionOutputOptions productionOutputOptions;
    private readonly IProductionRecordWriter productionRecordWriter;
    private readonly StationEnableStateStore stationEnableStateStore;
    private readonly StandardSampleState sampleState;
    private readonly IMessageBus messageBus;
    private readonly ILocalizationService localizationService;
    private readonly IDisposable stationLimitsAppliedSubscription;
    private readonly ObservableCollection<IDataGridColumnDescriptor> partColumns = [];
    private readonly ObservableCollection<HomeChartTabModel> chartTabs = [];
    private readonly ObservableCollection<IDataGridColumnDescriptor> tapeParameterColumns = [];
    private readonly ObservableCollection<TapeParameterRowModel> tapeParameterRows = [];
    private MesWorkOrderSetup? currentWorkOrderSetup;
    private bool areStationLimitsVisible;
    private long chartSampleSequence;
    private int chartLimitsSyncPending;
    private bool requiresLsLowerLimitOverride;
    private string specialMachineLsLowerLimitText = string.Empty;
    private string specialMachineLsUnit = string.Empty;
    private AsyncDelegateCommand? mesConnectionCommand;
    private AsyncDelegateCommand? startCommand;
    private AsyncDelegateCommand? stopCommand;
    private AsyncDelegateCommand? scanReelCommand;

    public HomeViewModel(
        MachineBase machine,
        IMachineDeviceContext devices,
        IProductionContext productionContext,
        ICyntecReelScanWorkflow reelScanWorkflow,
        MesConnectionStatus mesConnectionStatus,
        IMesConnection mesConnection,
        IRawInputBarcodeReceiver rawInputBarcodeReceiver,
        IAppNotificationService notificationService,
        IToastMessageService toastMessageService,
        IInputDialogService inputDialogService,
        ISecurityKeyChecker securityKeyChecker,
        IPermissionService permissionService,
        KwyLogService? logService,
        IMesTrackService mesTrackService,
        IMesWorkOrderService mesWorkOrderService,
        BraidOptionsStore braidOptionsStore,
        MarkPrintOptionsStore markPrintOptionsStore,
        IProductionOutputOptions productionOutputOptions,
        IProductionRecordWriter productionRecordWriter,
        StationEnableStateStore stationEnableStateStore,
        StandardSampleState sampleState,
        IMessageBus messageBus,
        ILocalizationService localizationService)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _ = devices ?? throw new ArgumentNullException(nameof(devices));
        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
        this.reelScanWorkflow = reelScanWorkflow ?? throw new ArgumentNullException(nameof(reelScanWorkflow));
        this.mesConnectionStatus = mesConnectionStatus ?? throw new ArgumentNullException(nameof(mesConnectionStatus));
        this.mesConnection = mesConnection ?? throw new ArgumentNullException(nameof(mesConnection));
        this.rawInputBarcodeReceiver = rawInputBarcodeReceiver ?? throw new ArgumentNullException(nameof(rawInputBarcodeReceiver));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.toastMessageService = toastMessageService ?? throw new ArgumentNullException(nameof(toastMessageService));
        this.inputDialogService = inputDialogService ?? throw new ArgumentNullException(nameof(inputDialogService));
        this.securityKeyChecker = securityKeyChecker ?? throw new ArgumentNullException(nameof(securityKeyChecker));
        this.permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        this.logService = logService;
        this.mesTrackService = mesTrackService ?? throw new ArgumentNullException(nameof(mesTrackService));
        this.mesWorkOrderService = mesWorkOrderService ?? throw new ArgumentNullException(nameof(mesWorkOrderService));
        this.braidOptionsStore = braidOptionsStore ?? throw new ArgumentNullException(nameof(braidOptionsStore));
        this.markPrintOptionsStore = markPrintOptionsStore ?? throw new ArgumentNullException(nameof(markPrintOptionsStore));
        this.productionOutputOptions = productionOutputOptions ?? throw new ArgumentNullException(nameof(productionOutputOptions));
        this.productionRecordWriter = productionRecordWriter ?? throw new ArgumentNullException(nameof(productionRecordWriter));
        this.stationEnableStateStore = stationEnableStateStore ?? throw new ArgumentNullException(nameof(stationEnableStateStore));
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
        this.messageBus = messageBus;
        ArgumentNullException.ThrowIfNull(messageBus);
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));


        SyncColumns();
        SyncTapeParameterColumns();
        SyncChartTabs();
        AttachStandardSampleLimitItemHandlers(sampleState.StandardSample.LimitItems);
        sampleState.StandardSample.LimitItems.CollectionChanged += OnStandardSampleLimitItemsChanged;
        RestoreHomeDisplayState();

        machine.TableChanged += OnMachineTableChanged;
        machine.StationResultPublished += OnStationResultPublished;
        machine.RunningStateChanged += OnMachineRunningStateChanged;
        this.productionContext.PropertyChanged += OnProductionContextPropertyChanged;
        this.rawInputBarcodeReceiver.BarcodeReceived += OnRawInputBarcodeReceived;
        this.localizationService.LanguageChanged += OnLanguageChanged;
        stationLimitsAppliedSubscription = messageBus.Subscribe<HomeViewModel, StationLimitsAppliedMessage>(
            this,
            static (viewModel, _) => viewModel.OnStationLimitsApplied());
        _ = RefreshStationEnabledStatesForHomeAsync();
    }

    public MachineBase CurrentMachine => machine;

    public MesConnectionStatus MesStatus => mesConnectionStatus;

    public string MachineId => machine.MachineId;

    public string MachineName => machine.MachineName;

    public ObservableCollection<IDataGridColumnDescriptor> PartColumns => partColumns;

    public ObservableCollection<DisplayRowItem> PartRows => machine.PartRows;

    public ObservableCollection<HomeChartTabModel> ChartTabs => chartTabs;

    public ObservableCollection<IDataGridColumnDescriptor> TapeParameterColumns => tapeParameterColumns;

    public ObservableCollection<TapeParameterRowModel> TapeParameterRows => tapeParameterRows;

    public ObservableCollection<StationEnableItemModel> StationInstrumentItems => stationEnableStateStore.Items;

    public string WorkOrderNo { get => productionContext.WorkOrderNo; set => productionContext.WorkOrderNo = value; }

    public string TablePaperCode { get => productionContext.TablePaperCode; set => productionContext.TablePaperCode = value; }

    public string TopCoverCode { get => productionContext.TopCoverCode; set => productionContext.TopCoverCode = value; }

    public string OperatorNo { get => productionContext.OperatorNo; set => productionContext.OperatorNo = value; }

    public string EquipmentNo { get => productionContext.EquipmentNo; set => productionContext.EquipmentNo = value; }

    public string MachineType { get => productionContext.MachineType; set => productionContext.MachineType = value; }

    public bool IsTrailingXMachineType
        => MachineType.Trim().EndsWith("X", StringComparison.OrdinalIgnoreCase);

    public string SpecialMachineLsLowerLimitText
    {
        get => specialMachineLsLowerLimitText;
        private set => SetProperty(ref specialMachineLsLowerLimitText, value);
    }

    public string SpecialMachineLsUnit
    {
        get => specialMachineLsUnit;
        private set => SetProperty(ref specialMachineLsUnit, value);
    }

    public string ReelMatNo { get => productionContext.ReelMatNo; set => productionContext.ReelMatNo = value; }

    public string BarcodeContent { get => productionContext.BarcodeContent; set => productionContext.BarcodeContent = value; }

    public string ReelTpNo { get => productionContext.ReelTpNo; set => productionContext.ReelTpNo = value; }

    public string ReelWorkOrderNo { get => productionContext.ReelWorkOrderNo; set => productionContext.ReelWorkOrderNo = value; }

    public string ReelId { get => productionContext.ReelId; set => productionContext.ReelId = value; }

    public ReelScanState ReelScanState => productionContext.ReelScanState;

    public bool IsMachineRunning => machine.IsRunning;

    public bool ShowReelMatNo
        => machine.HomeWorkOrderFields.Contains(HomeWorkOrderField.ReelMatNo);

    public AsyncDelegateCommand MesConnectionCommand => mesConnectionCommand ??= new AsyncDelegateCommand(ExecuteMesConnectionAsync);

    public AsyncDelegateCommand StartCommand => startCommand ??= new AsyncDelegateCommand(ExecuteStartAsync, CanExecuteStart);

    public AsyncDelegateCommand StopCommand => stopCommand ??= new AsyncDelegateCommand(ExecuteStopAsync, CanExecuteStop);

    public AsyncDelegateCommand ScanReelCommand => scanReelCommand ??= new AsyncDelegateCommand(ExecuteScanReelAsync);


    public void ClearDataGrid() => RunOnUi(machine.ClearDataGrid);

    private async Task ExecuteMesConnectionAsync()
    {
        if (MesStatus.State == MesConnectionState.Connecting)
        {
            await ShowWarningOnUiAsync(localizationService.T("Home.Message.MesConnecting", "MES is connecting, please wait."), localizationService.T("Home.Title.MesConnect", "MES Connect")).ConfigureAwait(false);
            return;
        }

        if (MesStatus.State == MesConnectionState.Online)
        {
            await DisconnectMesAsync().ConfigureAwait(false);
            return;
        }

        await ConnectMesAsync().ConfigureAwait(false);
    }

    private async Task ConnectMesAsync()
    {
        try
        {
            MesStatus.State = MesConnectionState.Connecting;
            MesStatus.Message = "Connecting MES.";
            MesResult result = await mesConnection.ConnectAsync(DestroyToken).ConfigureAwait(false);
            MesStatus.State = mesConnection.State;
            MesStatus.Message = result.Message;

            if (result.IsSuccess)
            {
                await ShowMessageOnUiAsync(localizationService.T("Home.Message.MesConnectSuccess", "MES connected."), localizationService.T("Home.Title.MesConnect", "MES Connect")).ConfigureAwait(false);
                return;
            }

            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.MesConnectFailed", "MES connect failed:\n{0}", result.Message), localizationService.T("Home.Title.MesConnect", "MES Connect")).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MesStatus.State = MesConnectionState.Faulted;
            MesStatus.Message = ex.Message;
            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.MesConnectException", "MES connect exception:\n{0}", ex.Message), localizationService.T("Home.Title.MesConnect", "MES Connect")).ConfigureAwait(false);
        }
    }

    private async Task DisconnectMesAsync()
    {
        if (!securityKeyChecker.IsPresent())
        {
            await ShowWarningOnUiAsync(localizationService.T("Home.Message.MesDisconnectNoKey", "No permission to disconnect MES."), localizationService.T("Home.Title.MesDisconnect", "MES Disconnect")).ConfigureAwait(false);
            return;
        }

        if (!permissionService.HasPermission(PermissionCodes.Engineer))
        {
            await ShowWarningOnUiAsync(localizationService.T("Home.Message.MesDisconnectEngineerRequired", "MES disconnect requires engineer permission."), localizationService.T("Home.Title.MesDisconnect", "MES Disconnect")).ConfigureAwait(false);
            return;
        }

        (permissionService as IPermissionUsageNotifier)?.NotifyPermissionUsed(PermissionCodes.Engineer);

        try
        {
            MesResult result = await mesConnection.DisconnectAsync(DestroyToken).ConfigureAwait(false);
            MesStatus.State = mesConnection.State;
            MesStatus.Message = result.Message;

            if (result.IsSuccess)
            {
                await ShowMessageOnUiAsync(localizationService.T("Home.Message.MesDisconnected", "MES disconnected."), localizationService.T("Home.Title.MesDisconnect", "MES Disconnect")).ConfigureAwait(false);
                return;
            }

            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.MesDisconnectFailed", "MES disconnect failed:\n{0}", result.Message), localizationService.T("Home.Title.MesDisconnect", "MES Disconnect")).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            MesStatus.Message = ex.Message;
            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.MesDisconnectException", "MES disconnect exception:\n{0}", ex.Message), localizationService.T("Home.Title.MesDisconnect", "MES Disconnect")).ConfigureAwait(false);
        }
    }

    private async Task ExecuteScanReelAsync()
    {
        try
        {
            await reelScanWorkflow.ScanAsync(DestroyToken).ConfigureAwait(false);
        }
        catch
        {
            // Reel scan failures are written to production context by the workflow; HomeView only displays state.
        }
    }

    private bool CanExecuteStart() => !machine.IsRunning;

    private bool CanExecuteStop() => machine.IsRunning;

    private async Task ExecuteStartAsync()
    {
        try
        {
            if (machine.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(WorkOrderNo))
            {
                await ShowWarningOnUiAsync(localizationService.T("Home.Message.WorkOrderRequired", "Please scan work order first."), localizationService.T("Home.Action.Start", "Start")).ConfigureAwait(false);
                return;
            }

            if (requiresLsLowerLimitOverride)
            {
                await ShowWarningOnUiAsync(
                    localizationService.T("Home.Message.LsLowerLimitRequired", "该工单需要重新输入 Ls 下限后才能启动。"),
                    localizationService.T("Home.Title.LsLowerLimit", "Ls 下限")).ConfigureAwait(false);
                return;
            }

            bool? isCheckCompleted = await machine.ReadCheckCompletedAsync(DestroyToken).ConfigureAwait(false);
            if (isCheckCompleted is false)
            {
                await ShowWarningOnUiAsync(
                    localizationService.T("Home.Message.CheckRequired", "请先完成点检，再启动生产。"),
                    localizationService.T("Home.Title.CheckRequired", "点检提醒")).ConfigureAwait(false);
                return;
            }

            MesResult<MesTrackResult> trackInResult = await mesTrackService.TrackInAsync(
                new MesTrackRequest(CreateMesContext(), GetCurrentUnitId(), WorkOrderNo),
                DestroyToken).ConfigureAwait(false);

            if (!IsMesAccepted(trackInResult))
            {
            await ShowErrorOnUiAsync(MesFailureMessageFormatter.Format(localizationService.T("Home.Title.MesTrackIn", "MES Track In"), trackInResult), localizationService.T("Home.Title.MesTrackIn", "MES Track In")).ConfigureAwait(false);
                return;
            }

            if (machine is IMachineWorkOrderStartSignalMachine workOrderStartSignalMachine)
            {
                workOrderStartSignalMachine.SetCurrentWorkOrder(WorkOrderNo);
            }

            await machine.StartAsync(DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.StartFailed", "Start failed:\n{0}", ex.Message), localizationService.T("Home.Title.StartFailed", "Start Failed")).ConfigureAwait(false);
        }
    }

    private async Task ExecuteStopAsync()
    {
        try
        {
            if (!machine.IsRunning)
            {
                return;
            }

            bool shouldTrackOut = await ShowConfirmOnUiAsync(
                localizationService.T("Home.Message.TrackOutConfirm", "Track out?"),
                localizationService.T("Home.Title.MesTrackOut", "MES Track Out")).ConfigureAwait(false);

            await machine.StopAsync().ConfigureAwait(false);
            if (machine is IMachineWorkOrderStartSignalMachine workOrderStartSignalMachine)
            {
                await workOrderStartSignalMachine.ResetWorkOrderStartSignalsAsync(DestroyToken).ConfigureAwait(false);
            }

            if (!shouldTrackOut)
            {
                return;
            }

            if (!await SaveProductionDataAsync(DestroyToken).ConfigureAwait(false))
            {
                return;
            }

            MesResult<MesTrackResult> trackOutResult = await mesTrackService.TrackOutAsync(
                new MesTrackOutRequest(
                    CreateMesContext(),
                    GetCurrentUnitId(),
                    WorkOrderNo,
                    Passed: true,
                    Measurements: BuildMeasurementResults()),
                DestroyToken).ConfigureAwait(false);

            if (trackOutResult.Exchange?.ReturnCode != 0)
            {
                await ShowErrorOnUiAsync(MesFailureMessageFormatter.Format(localizationService.T("Home.Title.MesTrackOut", "MES Track Out"), trackOutResult), localizationService.T("Home.Title.MesTrackOut", "MES Track Out")).ConfigureAwait(false);
                return;
            }

            if (machine is IMachineProductionCounterResetMachine counterResetMachine)
            {
                await counterResetMachine.ResetProductionCounterAsync(DestroyToken).ConfigureAwait(false);
            }
            ClearForNewWorkOrderScan();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.StopFailed", "Stop failed:\n{0}", ex.Message), localizationService.T("Home.Title.StopFailed", "Stop Failed")).ConfigureAwait(false);
        }
    }

    private async Task<bool> SaveProductionDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string fileName = ProductionRecordPathHelper.BuildFileName(WorkOrderNo, machine.MachineId);
        string outputDirectory = string.IsNullOrWhiteSpace(productionOutputOptions.OutputDirectory)
            ? @"D:\MES\Output"
            : productionOutputOptions.OutputDirectory;

        try
        {
            bool moved = await productionRecordWriter.MoveAsync(
                ProductionRecordPathHelper.RuntimeDirectory,
                fileName,
                outputDirectory,
                fileName,
                cancellationToken).ConfigureAwait(false);

            if (machine is IMachineProductionSummaryMachine summaryMachine)
            {
                await summaryMachine.SaveProductionSummaryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!moved)
            {
                logService?.Warn(localizationService.TF(
                    "Home.Log.RuntimeDataFileMissing",
                    "Production runtime data file was not found: {0}",
                    Path.Combine(ProductionRecordPathHelper.RuntimeDirectory, fileName)));
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ShowErrorOnUiAsync(localizationService.TF("Home.Message.ProductionDataSaveFailed", "Production data save failed:\n{0}", ex.Message), localizationService.T("Home.Title.ProductionDataSave", "Production Data Save")).ConfigureAwait(false);
            return false;
        }
    }

    private MesRequestContext CreateMesContext()
        => new(
            MachineId: string.IsNullOrWhiteSpace(EquipmentNo) ? machine.MachineId : EquipmentNo,
            MachineName: machine.MachineName,
            OperatorId: OperatorNo,
            WorkOrderNo: WorkOrderNo);

    private string GetCurrentUnitId()
    {
        if (!string.IsNullOrWhiteSpace(ReelId))
        {
            return ReelId;
        }

        if (!string.IsNullOrWhiteSpace(BarcodeContent))
        {
            return BarcodeContent;
        }

        return WorkOrderNo;
    }

    private IReadOnlyList<MesMeasurementResult> BuildMeasurementResults()
    {
        var results = new List<MesMeasurementResult>();
        foreach (TestStationModel station in machine.TestStations)
        {
            foreach (string testName in station.OrderedTestNames)
            {
                if (!station.TestValues.TryGetValue(testName, out double value))
                {
                    continue;
                }

                station.TestJudges.TryGetValue(testName, out bool passed);
                station.TestLimits.TryGetValue(testName, out StationMeasurementLimit? limit);
                results.Add(new MesMeasurementResult(
                    ParameterId: testName,
                    DisplayName: testName,
                    Value: value,
                    Passed: passed,
                    LowerLimit: limit?.LowerLimit,
                    UpperLimit: limit?.UpperLimit,
                    Unit: limit?.Unit));
            }
        }

        return results;
    }

    private async void OnRawInputBarcodeReceived(object? sender, BarcodeInputReceivedEventArgs e)
    {
        string value = e.Code.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        logService?.Info(localizationService.TF("Home.Log.BlindScanContent", "Blind scan content: {0}", value));

        try
        {
            await ApplyRawBarcodeAsync(value).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorOnUiAsync(ex.Message, localizationService.T("Home.Title.BlindScan", "Blind Scan")).ConfigureAwait(false);
        }
    }

    private async Task ApplyRawBarcodeAsync(string value)
    {
        if (machine.ProductionState != MachineProductionState.Stopped)
        {
            await ShowWarningOnUiAsync(
                localizationService.T("Home.Message.BlindScanRequiresStopped", "机台非停止状态，禁止盲扫修改生产信息。"),
                localizationService.T("Home.Title.BlindScan", "盲扫")).ConfigureAwait(false);
            return;
        }

        toastMessageService.ShowInfo(localizationService.TF("Home.Message.BlindScanReceived", "Blind scan: {0}", value));

        switch (value.Length)
        {
            case 6 when value.All(char.IsDigit):
                await ShowWarningOnUiAsync(localizationService.TF("Home.Message.SixDigitInvalid", "Pure 6-digit value {0} is invalid.", value), localizationService.T("Home.Title.BlindScan", "Blind Scan")).ConfigureAwait(false);
                break;
            case 6:
                if (!value.StartsWith("TP", StringComparison.OrdinalIgnoreCase))
                {
                    await ShowWarningOnUiAsync(localizationService.TF("Home.Message.EquipmentNoPrefixInvalid", "Equipment no {0} does not start with TP.", value), localizationService.T("Home.Title.BlindScan", "Blind Scan")).ConfigureAwait(false);
                    return;
                }

                EquipmentNo = value;
                break;
            case 8:
                OperatorNo = CleanRawBarcodeValue(value);
                break;
            case 50:
                OperatorNo = CleanOperatorBarcode(value);
                break;
            case 12:
                string previousMachineType = MachineType;
                string workOrderNo = CleanWorkOrderBarcode(value);
                ClearForNewWorkOrderScan(clearSampleState: false);
                await ResetProductionCounterForNewWorkOrderAsync().ConfigureAwait(false);
                WorkOrderNo = workOrderNo;
                await LoadWorkOrderSetupAsync(workOrderNo, previousMachineType).ConfigureAwait(false);
                break;
            case 76:
            case 84:
            case 89:
            case 120:
            case 123:
                await ApplyCoverOrTablePaperAsync(value).ConfigureAwait(false);
                break;
            case 116:
                await ApplyReelMaterialAsync(value).ConfigureAwait(false);
                break;
            default:
                toastMessageService.ShowError(localizationService.TF(
                    "Home.Message.BlindScanUnsupportedLength",
                    "Blind scan content: {0}; unsupported length: {1}.",
                    value,
                    value.Length));
                break;
        }
    }

    private async Task ResetProductionCounterForNewWorkOrderAsync()
    {
        if (machine is not IMachineProductionCounterResetMachine counterResetMachine)
        {
            return;
        }

        try
        {
            await counterResetMachine.ResetProductionCounterAsync(DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logService?.Warn(localizationService.TF(
                "Home.Log.CounterResetFailed",
                "Production counter reset failed while scanning new work order: {0}",
                ex.Message));
        }
    }
    private void ClearForNewWorkOrderScan(bool clearSampleState = true)
        => RunOnUi(() =>
        {
            machine.ClearDataGrid();
            WorkOrderNo = string.Empty;
            SpecialMachineLsLowerLimitText = string.Empty;
            SpecialMachineLsUnit = string.Empty;
            TablePaperCode = string.Empty;
            TopCoverCode = string.Empty;
            OperatorNo = string.Empty;
            EquipmentNo = string.Empty;
            MachineType = string.Empty;
            ReelMatNo = string.Empty;
            BarcodeContent = string.Empty;
            ReelTpNo = string.Empty;
            ReelWorkOrderNo = string.Empty;
            ReelId = string.Empty;
            productionContext.ReelScanState = ReelScanState.None;
            productionContext.IsResultGridDataEnabled = false;
            areStationLimitsVisible = false;
            ClearChartLimits();
            SyncTapeParameterRows(null);
            if (clearSampleState)
            {
                ClearSampleState();
            }
        });

    private async Task LoadWorkOrderSetupAsync(string workOrderNo, string? previousMachineType = null)
    {
        MesResult<MesWorkOrderSetup> result = await mesWorkOrderService.GetWorkOrderSetupAsync(
            new MesWorkOrderRequest(CreateMesContext(), workOrderNo),
            DestroyToken).ConfigureAwait(false);

        if (!IsMesAccepted(result) || result.Data == null)
        {
            await ShowErrorOnUiAsync(MesFailureMessageFormatter.Format(localizationService.TF("Home.Message.WorkOrderImportWithNo", "Work order {0} import", workOrderNo), result), localizationService.T("Home.Title.WorkOrderImport", "Work Order Import")).ConfigureAwait(false);
            return;
        }

        currentWorkOrderSetup = result.Data;
        areStationLimitsVisible = true;
        await machine.ApplyWorkOrderSetupAsync(result.Data, DestroyToken).ConfigureAwait(false);
        await SaveBraidOptionsAsync(result.Data.TapeSetup).ConfigureAwait(false);
        await SaveMarkPrintOptionsAsync(result.Data).ConfigureAwait(false);
        RunOnUi(() =>
        {
            machine.RefreshResultGrid();
            SyncColumns();
            SyncChartTabs();
        });
        string newMachineType = GetWorkOrderMachineType(result.Data);
        if (ShouldClearSampleStateForMachineTypeChange(previousMachineType, newMachineType))
        {
            RunOnUi(ClearSampleState);
        }
        ApplyWorkOrderSetup(result.Data);
        productionContext.IsResultGridDataEnabled = true;
        SyncChartLimits();
        SyncTapeParameterRows(result.Data.TapeSetup);
        await ShowMessageOnUiAsync(localizationService.TF("Home.Message.WorkOrderImportSuccess", "Work order {0} imported.", workOrderNo), localizationService.T("Home.Title.WorkOrderImport", "Work Order Import")).ConfigureAwait(false);
        await ApplyTrailingXMachineTypeLsLowerLimitAsync(result.Data).ConfigureAwait(false);
    }

    private async Task ApplyTrailingXMachineTypeLsLowerLimitAsync(MesWorkOrderSetup setup)
    {
        requiresLsLowerLimitOverride = false;
        // X 机种以 MES 解析后写入 HomeView 的机种标识为准，而不是工单号。
        string displayedMachineType = MachineType.Trim();
        if (!displayedMachineType.EndsWith("X", StringComparison.OrdinalIgnoreCase))
        {
            SpecialMachineLsLowerLimitText = string.Empty;
            SpecialMachineLsUnit = string.Empty;
            return;
        }

        logService?.Info(localizationService.TF(
            "Home.Log.TrailingXMachineType",
            "Trailing-X machine type detected. MachineType={0}",
            displayedMachineType));

        MesWorkOrderInstrumentSetup? lsSetup = setup.InstrumentSetups?.FirstOrDefault(item =>
            string.Equals(item.ParameterId, "Ls", StringComparison.OrdinalIgnoreCase));
        if (lsSetup == null)
        {
            await ShowWarningOnUiAsync(
                localizationService.T("Home.Message.LsSetupMissing", "该工单未提供 Ls 配置，无法重新设置 Ls 下限。"),
                localizationService.T("Home.Title.LsLowerLimit", "Ls 下限")).ConfigureAwait(false);
            requiresLsLowerLimitOverride = true;
            return;
        }

        // 工单导入来自扫码异步链路，前面使用 ConfigureAwait(false) 后不保证仍在 UI 线程；
        // InputDialogService 不负责切换线程，必须在 UI Dispatcher 上创建输入框。
        InputDialogResult input = await InvokeOnUiAsync(() => inputDialogService.ShowAsync(new InputDialogOptions
        {
            Title = localizationService.T("Home.Title.LsLowerLimit", "Ls 下限"),
            ShowContentTitle = false,
            Message = localizationService.T("Home.Message.LsLowerLimitInput", "该工单以 X 结尾，请重新输入电感 Ls 下限。"),
            Label = localizationService.T("Home.Field.LsLowerLimit", "Ls 下限"),
            InputType = InputDialogType.Number,
            Minimum = 0,
            Unit = lsSetup.Unit,
            ConfirmButtonText = localizationService.T("Common.Confirm", "确定"),
            CancelButtonText = localizationService.T("Common.Cancel", "取消"),
            ShowCancelButton = false
        })).ConfigureAwait(false);

        if (!input.IsConfirmed || !input.TryGetDecimal(out decimal lowerLimit))
        {
            requiresLsLowerLimitOverride = true;
            await ShowWarningOnUiAsync(
                localizationService.T("Home.Message.LsLowerLimitRequired", "该工单需要重新输入 Ls 下限后才能启动。"),
                localizationService.T("Home.Title.LsLowerLimit", "Ls 下限")).ConfigureAwait(false);
            return;
        }

        MesWorkOrderInstrumentSetup[] instrumentSetups = (setup.InstrumentSetups ?? [])
            .Select(item => string.Equals(item.ParameterId, "Ls", StringComparison.OrdinalIgnoreCase)
                ? item with { LowerLimit = (double)lowerLimit }
                : item)
            .ToArray();
        MesWorkOrderSetup overriddenSetup = setup with { InstrumentSetups = instrumentSetups };

        await machine.ApplyWorkOrderSetupAsync(overriddenSetup, DestroyToken).ConfigureAwait(false);
        currentWorkOrderSetup = overriddenSetup;
        SpecialMachineLsLowerLimitText = lowerLimit.ToString("G29", CultureInfo.CurrentCulture);
        SpecialMachineLsUnit = lsSetup.Unit?.Trim() ?? string.Empty;
        RunOnUi(() =>
        {
            machine.RefreshResultGrid();
            SyncColumns();
            SyncChartTabs();
        });
        SyncChartLimits();
        messageBus.Publish(new StationLimitsAppliedMessage());
    }

    private async Task SaveBraidOptionsAsync(MesWorkOrderTapeSetup? tapeSetup)
    {
        if (tapeSetup == null)
        {
            return;
        }

        await braidOptionsStore.SaveAsync(BraidOptions.FromTapeSetup(tapeSetup)).ConfigureAwait(false);
    }

    private async Task SaveMarkPrintOptionsAsync(MesWorkOrderSetup setup)
    {
        if (machine is not IMachineMarkPrintOptionsMachine markPrintMachine)
        {
            return;
        }

        setup.Parameters.TryGetString("MarkPrintString", out string printString);
        await markPrintOptionsStore.SaveAsync(new MarkPrintOptions
        {
            PrintString = printString
        }).ConfigureAwait(false);

        try
        {
            await markPrintMachine.ApplyMarkPrintStringAsync(printString, DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await notificationService.ErrorAsync(
                localizationService.T("Home.Message.MarkPrintFailed", "缂栧甫瀛楃鍐欏叆鎵撳嵃鏈哄け璐ワ紒"),
                localizationService.T("Home.Title.MarkPrint", "缂栧甫瀛楃"),
                ex).ConfigureAwait(false);
        }
    }

    private async Task ApplyCoverOrTablePaperAsync(string value)
    {
        MesWorkOrderMaterialRequirements? requirements = currentWorkOrderSetup?.MaterialRequirements;
        if (requirements == null)
        {
            return;
        }

        string materialNo = CleanMaterialBarcode(value);
        if (string.IsNullOrWhiteSpace(materialNo))
        {
            return;
        }

        if (MaterialNoMatches(materialNo, requirements.TablePaperMatNo))
        {
            TablePaperCode = materialNo;
            return;
        }

        if (MaterialNoMatches(materialNo, requirements.TopCoverMatNo))
        {
            TopCoverCode = materialNo;
            return;
        }

        await ShowWarningOnUiAsync(localizationService.T("Home.Message.CoverOrPaperMismatch", "Top cover or table paper does not match MES."), localizationService.T("Home.Title.MaterialCheck", "Material Check")).ConfigureAwait(false);
    }


    private async Task ApplyReelMaterialAsync(string value)
    {
        MesWorkOrderMaterialRequirements? requirements = currentWorkOrderSetup?.MaterialRequirements;
        if (requirements == null)
        {
            return;
        }

        string materialNo = CleanMaterialBarcode(value);
        if (string.IsNullOrWhiteSpace(materialNo))
        {
            return;
        }

        if (MaterialNoMatches(materialNo, requirements.ReelMatNo))
        {
            ReelMatNo = materialNo;
            return;
        }

        await ShowWarningOnUiAsync(localizationService.T("Home.Message.ReelMatMismatch", "Reel material does not match MES."), localizationService.T("Home.Title.MaterialError", "Material Error")).ConfigureAwait(false);
    }
    private static string CleanRawBarcodeValue(string value)
        => value.Trim();

    private static string CleanWorkOrderBarcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormKC);
        return new string(normalized
            .Where(static character => !char.IsControl(character)
                && character is not '\u200B' and not '\uFEFF')
            .ToArray())
            .Trim();
    }

    private static string CleanOperatorBarcode(string value)
    {
        string[] parts = value.Split('{');
        return parts.Length > 4 ? parts[4].Trim() : CleanRawBarcodeValue(value);
    }

    private static string CleanMaterialBarcode(string value)
    {
        string[] parts = value.Split('{');
        return parts.Length > 0 ? parts[0].Trim() : CleanRawBarcodeValue(value);
    }

    private static bool MaterialNoMatches(string materialNo, string? expectedMaterialNos)
    {
        if (string.IsNullOrWhiteSpace(expectedMaterialNos))
        {
            return false;
        }

        return expectedMaterialNos
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(expected => string.Equals(materialNo, expected, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyWorkOrderSetup(MesWorkOrderSetup setup)
    {
        string machineType = GetWorkOrderMachineType(setup);
        if (!string.IsNullOrWhiteSpace(machineType))
        {
            RunOnUi(() => MachineType = machineType);
        }
    }

    private static string GetWorkOrderMachineType(MesWorkOrderSetup setup)
        => setup.EquipmentType?.Trim() ?? string.Empty;

    private static bool ShouldClearSampleStateForMachineTypeChange(string? previousMachineType, string newMachineType)
        => !string.IsNullOrWhiteSpace(previousMachineType)
            && !string.IsNullOrWhiteSpace(newMachineType)
            && !string.Equals(previousMachineType.Trim(), newMachineType.Trim(), StringComparison.OrdinalIgnoreCase);

    private void ClearSampleState()
    {
        sampleState.ClearAll();
        messageBus.Publish(new ProductionContextClearedMessage());
    }

    private void RestoreHomeDisplayState()
    {
        if (!productionContext.IsResultGridDataEnabled)
        {
            machine.ClearDataGrid();
            areStationLimitsVisible = false;
            ClearChartLimits();
            SyncTapeParameterRows(null);
            return;
        }

        areStationLimitsVisible = true;
        SyncChartLimits();
        SyncTapeParameterRows(braidOptionsStore.Current.ToTapeSetup());
    }
    private void SyncColumns()
        => RunOnUi(() =>
        {
            partColumns.Clear();
            foreach (IDataGridColumnDescriptor column in machine.PartColumns)
            {
                partColumns.Add(column);
            }
        });

    private void SyncTapeParameterColumns()
        => RunOnUi(() =>
        {
            tapeParameterColumns.Clear();
            tapeParameterColumns.Add(CreateTapeParameterColumn(nameof(TapeParameterRowModel.BeforeSpaceQty), localizationService.T("Braid.BeforeSpaceQty", "Before Space")));
            tapeParameterColumns.Add(CreateTapeParameterColumn(nameof(TapeParameterRowModel.PackageQty), localizationService.T("Braid.PackageQty", "Package Qty")));
            tapeParameterColumns.Add(CreateTapeParameterColumn(nameof(TapeParameterRowModel.AfterSpaceQty), localizationService.T("Braid.AfterSpaceQty", "After Space")));
            tapeParameterColumns.Add(CreateTapeParameterColumn(nameof(TapeParameterRowModel.SampleQty), localizationService.T("Braid.SampleQty", "Sample Qty")));
            tapeParameterColumns.Add(CreateTapeParameterColumn(nameof(TapeParameterRowModel.BlankQty), localizationService.T("Braid.BlankQty", "Blank Qty")));
            tapeParameterColumns.Add(CreateTapeParameterColumn(nameof(TapeParameterRowModel.BackNoFilmQty), localizationService.T("Braid.BackNoFilmQty", "Back No Film")));
        });

    private static IDataGridColumnDescriptor CreateTapeParameterColumn(string bindingPath, string displayName)
        => new WpfDataGridColumnOptions
        {
            ParameterId = bindingPath,
            DisplayName = displayName,
            BindingPath = bindingPath,
            ElementStyleKey = "TapeParameterCellTextBlockStyle",
            CanUserSort = false,
            CanUserResize = false,
            CanUserReorder = false
        };

    private void SyncChartTabs()
        => RunOnUi(() =>
        {
            chartTabs.Clear();
            foreach (string testName in machine.TestStations
                         .SelectMany(station => station.OrderedTestNames)
                         .Where(static name => !string.IsNullOrWhiteSpace(name))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                chartTabs.Add(new HomeChartTabModel
                {
                    ParameterId = testName,
                    DisplayName = testName
                });
            }
        });

    private void SyncChartLimits()
    {
        if (!areStationLimitsVisible)
        {
            ClearChartLimits();
            return;
        }

        RunOnUi(() =>
        {
            foreach (HomeChartTabModel tab in chartTabs)
            {
                StationMeasurementLimit? limit = machine.TestStations
                    .Select(station => station.TestLimits.TryGetValue(tab.ParameterId, out StationMeasurementLimit? value) ? value : null)
                    .FirstOrDefault(value => value != null);

                double? targetValue = GetStandardSampleTargetValue(tab.ParameterId);
                tab.Limits = limit == null && targetValue == null
                    ? null
                    : new ChartLimitSet(limit?.LowerLimit, limit?.UpperLimit, targetValue);
            }
        });
    }


    private double? GetStandardSampleTargetValue(string parameterId)
    {
        foreach (string code in NormalizeSampleLimitCodes(parameterId))
        {
            StandardSampleLimitItemModel? item = sampleState.StandardSample.LimitItems
                .FirstOrDefault(candidate => IsSameSampleLimit(candidate, code));

            double? targetValue = TryParseNullableDouble(item?.StandardValue);
            if (targetValue.HasValue)
            {
                return targetValue;
            }
        }

        return null;
    }

    private static bool IsSameSampleLimit(StandardSampleLimitItemModel item, string code)
        => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.DisplayName, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeSampleLimitCode(item.Code), code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeSampleLimitCode(item.DisplayName), code, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> NormalizeSampleLimitCodes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string part in value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? code = NormalizeSampleLimitCode(part);
            if (!string.IsNullOrWhiteSpace(code))
            {
                yield return code;
            }
        }
    }

    private static string? NormalizeSampleLimitCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string code = value.Trim().ToUpperInvariant();
        while (code.Length > 0 && char.IsDigit(code[^1]))
        {
            code = code[..^1];
        }

        return string.IsNullOrWhiteSpace(code) ? null : code;
    }

    private void OnStandardSampleLimitItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (StandardSampleLimitItemModel item in e.OldItems)
            {
                item.PropertyChanged -= OnStandardSampleLimitItemPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (StandardSampleLimitItemModel item in e.NewItems)
            {
                item.PropertyChanged += OnStandardSampleLimitItemPropertyChanged;
            }
        }

        SyncChartLimits();
    }

    private void OnStandardSampleLimitItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StandardSampleLimitItemModel.StandardValue)
            or nameof(StandardSampleLimitItemModel.LowerLimit)
            or nameof(StandardSampleLimitItemModel.UpperLimit))
        {
            SyncChartLimits();
        }
    }

    private void AttachStandardSampleLimitItemHandlers(IEnumerable<StandardSampleLimitItemModel> items)
    {
        foreach (StandardSampleLimitItemModel item in items)
        {
            item.PropertyChanged += OnStandardSampleLimitItemPropertyChanged;
        }
    }

    private void DetachStandardSampleLimitItemHandlers(IEnumerable<StandardSampleLimitItemModel> items)
    {
        foreach (StandardSampleLimitItemModel item in items)
        {
            item.PropertyChanged -= OnStandardSampleLimitItemPropertyChanged;
        }
    }

    private static double? TryParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariantValue))
        {
            return invariantValue;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double currentValue)
            ? currentValue
            : null;
    }

    private void ClearChartLimits()
        => RunOnUi(() =>
        {
            foreach (HomeChartTabModel tab in chartTabs)
            {
                tab.Limits = null;
            }
        });

    private async Task RefreshStationEnabledStatesForHomeAsync()
    {
        try
        {
            await stationEnableStateStore.RefreshFromPlcAsync(DestroyToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
    private void SyncTapeParameterRows(MesWorkOrderTapeSetup? tapeSetup)
        => RunOnUi(() =>
        {
            tapeParameterRows.Clear();
            if (tapeSetup == null)
            {
                return;
            }

            tapeParameterRows.Add(new TapeParameterRowModel
            {
                BeforeSpaceQty = FormatNullableInt(tapeSetup.BeforeSpaceQty),
                PackageQty = FormatNullableInt(tapeSetup.PackageQty),
                AfterSpaceQty = FormatNullableInt(tapeSetup.AfterSpaceQty),
                SampleQty = FormatNullableInt(tapeSetup.SampleQty),
                BlankQty = FormatNullableInt(tapeSetup.BlankQty),
                BackNoFilmQty = FormatNullableInt(tapeSetup.BackNoFilmQty)
            });
        });
    private void OnMachineRunningStateChanged(object? sender, EventArgs e)
        => RunOnUi(RefreshMachineRunningState);

    private void RefreshMachineRunningState()
    {
        RaisePropertyChanged(nameof(IsMachineRunning));
        startCommand?.RaiseCanExecuteChanged();
        stopCommand?.RaiseCanExecuteChanged();
    }
    private void OnMachineTableChanged(object? sender, EventArgs e)
        => RequestChartLimitsSync();

    private void OnStationResultPublished(object? sender, StationResultPublishedEventArgs e)
        => PostOnUi(() => PushChartSamples(e), DispatcherPriority.Render);

    private void OnStationLimitsApplied()
    {
        productionContext.IsResultGridDataEnabled = true;
        areStationLimitsVisible = true;
        SyncChartLimits();
    }

    private void RequestChartLimitsSync()
    {
        if (Interlocked.Exchange(ref chartLimitsSyncPending, 1) == 1)
        {
            return;
        }

        PostOnUi(() =>
        {
            Volatile.Write(ref chartLimitsSyncPending, 0);
            SyncChartLimits();
        });
    }

    private void PushChartSamples(StationResultPublishedEventArgs e)
    {
        foreach (TestResultPayload value in e.Values)
        {
            HomeChartTabModel? tab = chartTabs.FirstOrDefault(item => string.Equals(item.ParameterId, value.Name, StringComparison.OrdinalIgnoreCase));
            if (tab == null)
            {
                continue;
            }

            tab.AddSample(new ChartValueSample(++chartSampleSequence, value.TestValue, value.Judge));
        }
    }
    private void OnLanguageChanged(object? sender, LanguageType languageType)
    {
        SyncColumns();
        SyncTapeParameterColumns();
    }

    private void OnProductionContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IProductionContext.WorkOrderNo):
                RaisePropertyChanged(nameof(WorkOrderNo));
                break;
            case nameof(IProductionContext.TablePaperCode):
                RaisePropertyChanged(nameof(TablePaperCode));
                break;
            case nameof(IProductionContext.TopCoverCode):
                RaisePropertyChanged(nameof(TopCoverCode));
                break;
            case nameof(IProductionContext.OperatorNo):
                RaisePropertyChanged(nameof(OperatorNo));
                break;
            case nameof(IProductionContext.EquipmentNo):
                RaisePropertyChanged(nameof(EquipmentNo));
                break;
            case nameof(IProductionContext.MachineType):
                RaisePropertyChanged(nameof(MachineType));
                RaisePropertyChanged(nameof(IsTrailingXMachineType));
                break;
            case nameof(IProductionContext.ReelMatNo):
                RaisePropertyChanged(nameof(ReelMatNo));
                break;
            case nameof(IProductionContext.BarcodeContent):
                RaisePropertyChanged(nameof(BarcodeContent));
                break;
            case nameof(IProductionContext.ReelTpNo):
                RaisePropertyChanged(nameof(ReelTpNo));
                break;
            case nameof(IProductionContext.ReelWorkOrderNo):
                RaisePropertyChanged(nameof(ReelWorkOrderNo));
                break;
            case nameof(IProductionContext.ReelId):
                RaisePropertyChanged(nameof(ReelId));
                break;
            case nameof(IProductionContext.ReelScanState):
                RaisePropertyChanged(nameof(ReelScanState));
                break;
        }
    }

    private static bool IsMesAccepted(MesResult<MesTrackResult> result)
        => (result.Exchange?.ReturnCode == 0 || result.IsSuccess) && (result.Data?.Accepted ?? result.IsSuccess);

    private static bool IsMesAccepted<T>(MesResult<T> result)
        => result.Exchange?.ReturnCode == 0 || result.IsSuccess;

    private static string? FormatNullableInt(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private Task<bool> ShowConfirmOnUiAsync(string message, string title)
        => notificationService.ConfirmAsync(message, title);

    private Task ShowMessageOnUiAsync(string message, string title)
        => notificationService.InfoAsync(message, title);

    private Task ShowWarningOnUiAsync(string message, string title)
        => notificationService.WarningAsync(message, title);

    private Task ShowErrorOnUiAsync(string message, string title)
        => notificationService.ErrorAsync(message, title);

    /// <summary>
    /// Ensures dialogs are shown from switch branches.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="action"></param>
    /// <returns></returns>
    private static Task<T> InvokeOnUiAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }


    private static void PostOnUi(Action action, DispatcherPriority priority = DispatcherPriority.Background)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            dispatcher.BeginInvoke(action, priority);
        }
        catch (InvalidOperationException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            machine.TableChanged -= OnMachineTableChanged;
            machine.StationResultPublished -= OnStationResultPublished;
            machine.RunningStateChanged -= OnMachineRunningStateChanged;
            productionContext.PropertyChanged -= OnProductionContextPropertyChanged;
            rawInputBarcodeReceiver.BarcodeReceived -= OnRawInputBarcodeReceived;
            localizationService.LanguageChanged -= OnLanguageChanged;
            sampleState.StandardSample.LimitItems.CollectionChanged -= OnStandardSampleLimitItemsChanged;
            DetachStandardSampleLimitItemHandlers(sampleState.StandardSample.LimitItems);
            stationLimitsAppliedSubscription.Dispose();
        }

        base.Dispose(disposing);
    }
}








