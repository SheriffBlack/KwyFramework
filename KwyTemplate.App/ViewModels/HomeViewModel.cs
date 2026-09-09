using Kwy.MVVM.Core;
using Kwy.MVVM.Messaging;
using Kwy.Device.Abstractions.Instrument;
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
using KwyTemplate.Contracts.Navigation;

namespace KwyTemplate.App.ViewModels;

public sealed class HomeViewModel : BindableBase
{
    private readonly MachineBase machine;
    private readonly IProductionContext productionContext;
    private readonly IPrimaryNavigationState primaryNavigationState;
    private readonly ICyntecReelScanWorkflow reelScanWorkflow;
    private readonly MesConnectionStatus mesConnectionStatus;
    private readonly IMesConnection mesConnection;
    private readonly IRawInputBarcodeReceiver rawInputBarcodeReceiver;
    private readonly IAppNotificationService notificationService;
    private readonly IToastMessageService toastMessageService;
    private readonly IInputDialogService inputDialogService;
    private readonly ISecurityKeyChecker securityKeyChecker;
    private readonly ICameraStartupOptionsDialogService cameraStartupOptionsDialogService;
    private readonly KwyLogService? logService;
    private readonly IMesTrackService mesTrackService;
    private readonly IMesWorkOrderService mesWorkOrderService;
    private readonly BraidOptionsStore braidOptionsStore;
    private readonly MarkPrintOptionsStore markPrintOptionsStore;
    private readonly IProductionOutputOptions productionOutputOptions;
    private readonly IProductionRecordWriter productionRecordWriter;
    private readonly IProductionDataArchiveService productionDataArchiveService;
    private readonly StationEnableStateStore stationEnableStateStore;
    private readonly StandardSampleState sampleState;
    private readonly IMessageBus messageBus;
    private readonly ILocalizationService localizationService;
    private readonly LocalWorkOrderRecipeSession localWorkOrderRecipeSession;
    private readonly LocalWorkOrderRecipeStore localWorkOrderRecipeStore;
    private readonly LocalWorkOrderRecipeMapper localWorkOrderRecipeMapper;
    private readonly ICorrectionParameterProvider correctionParameterProvider;
    private readonly IMachineDeviceContext devices;
    private readonly IDisposable stationLimitsAppliedSubscription;
    private readonly IDisposable localWorkOrderRecipeLoadedSubscription;
    private readonly object mesStateSyncRoot = new();
    private readonly ObservableCollection<IDataGridColumnDescriptor> partColumns = [];
    private readonly ObservableCollection<HomeChartTabModel> chartTabs = [];
    private readonly ObservableCollection<IDataGridColumnDescriptor> tapeParameterColumns = [];
    private readonly ObservableCollection<TapeParameterRowModel> tapeParameterRows = [];
    private MesWorkOrderSetup? currentWorkOrderSetup;
    private bool areStationLimitsVisible;
    private long chartSampleSequence;
    private int chartLimitsSyncPending;
    private bool requiresLsLowerLimitOverride;
    private MesConnectionState lastMesConnectionState;
    private TaskCompletionSource? mesConnectSuccessDialogCompletion;
    private int onlineWorkOrderRefreshPending;
    // MES 从在线切到离线后，现有 Home 字段只保留展示；必须重新扫工单才允许加载本地机种配方。
    private bool offlineWorkOrderRescanRequired;
    // Home 在结束工单时会清空显示字段；该快照只用于判断下一次机种是否变化。
    private string lastResolvedMachineType = string.Empty;
    // X 机种 Ls 下限确认与工单号无关；同一机种在离线扫码顺序变化或启动重载时不应重复弹窗。
    private string confirmedTrailingXMachineType = string.Empty;
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
        IPrimaryNavigationState primaryNavigationState,
        ICyntecReelScanWorkflow reelScanWorkflow,
        MesConnectionStatus mesConnectionStatus,
        IMesConnection mesConnection,
        IRawInputBarcodeReceiver rawInputBarcodeReceiver,
        IAppNotificationService notificationService,
        IToastMessageService toastMessageService,
        IInputDialogService inputDialogService,
        ISecurityKeyChecker securityKeyChecker,
        ICameraStartupOptionsDialogService cameraStartupOptionsDialogService,
        KwyLogService? logService,
        IMesTrackService mesTrackService,
        IMesWorkOrderService mesWorkOrderService,
        BraidOptionsStore braidOptionsStore,
        MarkPrintOptionsStore markPrintOptionsStore,
        IProductionOutputOptions productionOutputOptions,
        IProductionRecordWriter productionRecordWriter,
        IProductionDataArchiveService productionDataArchiveService,
        StationEnableStateStore stationEnableStateStore,
        StandardSampleState sampleState,
        IMessageBus messageBus,
        ILocalizationService localizationService,
        LocalWorkOrderRecipeSession localWorkOrderRecipeSession,
        LocalWorkOrderRecipeStore localWorkOrderRecipeStore,
        LocalWorkOrderRecipeMapper localWorkOrderRecipeMapper,
        ICorrectionParameterProvider correctionParameterProvider)
    {
        this.machine = machine ?? throw new ArgumentNullException(nameof(machine));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.productionContext = productionContext ?? throw new ArgumentNullException(nameof(productionContext));
        this.primaryNavigationState = primaryNavigationState ?? throw new ArgumentNullException(nameof(primaryNavigationState));
        this.reelScanWorkflow = reelScanWorkflow ?? throw new ArgumentNullException(nameof(reelScanWorkflow));
        this.mesConnectionStatus = mesConnectionStatus ?? throw new ArgumentNullException(nameof(mesConnectionStatus));
        this.mesConnection = mesConnection ?? throw new ArgumentNullException(nameof(mesConnection));
        lastMesConnectionState = this.mesConnection.State;
        this.rawInputBarcodeReceiver = rawInputBarcodeReceiver ?? throw new ArgumentNullException(nameof(rawInputBarcodeReceiver));
        this.notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        this.toastMessageService = toastMessageService ?? throw new ArgumentNullException(nameof(toastMessageService));
        this.inputDialogService = inputDialogService ?? throw new ArgumentNullException(nameof(inputDialogService));
        this.securityKeyChecker = securityKeyChecker ?? throw new ArgumentNullException(nameof(securityKeyChecker));
        this.cameraStartupOptionsDialogService = cameraStartupOptionsDialogService ?? throw new ArgumentNullException(nameof(cameraStartupOptionsDialogService));
        this.logService = logService;
        this.mesTrackService = mesTrackService ?? throw new ArgumentNullException(nameof(mesTrackService));
        this.mesWorkOrderService = mesWorkOrderService ?? throw new ArgumentNullException(nameof(mesWorkOrderService));
        this.braidOptionsStore = braidOptionsStore ?? throw new ArgumentNullException(nameof(braidOptionsStore));
        this.braidOptionsStore.OptionsChanged += OnBraidOptionsChanged;
        this.markPrintOptionsStore = markPrintOptionsStore ?? throw new ArgumentNullException(nameof(markPrintOptionsStore));
        this.productionOutputOptions = productionOutputOptions ?? throw new ArgumentNullException(nameof(productionOutputOptions));
        this.productionRecordWriter = productionRecordWriter ?? throw new ArgumentNullException(nameof(productionRecordWriter));
        this.productionDataArchiveService = productionDataArchiveService ?? throw new ArgumentNullException(nameof(productionDataArchiveService));
        this.stationEnableStateStore = stationEnableStateStore ?? throw new ArgumentNullException(nameof(stationEnableStateStore));
        this.sampleState = sampleState ?? throw new ArgumentNullException(nameof(sampleState));
        this.messageBus = messageBus;
        ArgumentNullException.ThrowIfNull(messageBus);
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.localWorkOrderRecipeSession = localWorkOrderRecipeSession ?? throw new ArgumentNullException(nameof(localWorkOrderRecipeSession));
        this.localWorkOrderRecipeStore = localWorkOrderRecipeStore ?? throw new ArgumentNullException(nameof(localWorkOrderRecipeStore));
        this.localWorkOrderRecipeMapper = localWorkOrderRecipeMapper ?? throw new ArgumentNullException(nameof(localWorkOrderRecipeMapper));
        this.correctionParameterProvider = correctionParameterProvider ?? throw new ArgumentNullException(nameof(correctionParameterProvider));
        SyncColumns();
        SyncTapeParameterColumns();
        SyncChartTabs();
        AttachStandardSampleLimitItemHandlers(sampleState.StandardSample.LimitItems);
        sampleState.StandardSample.LimitItems.CollectionChanged += OnStandardSampleLimitItemsChanged;
        RestoreHomeDisplayState();

        machine.TableChanged += OnMachineTableChanged;
        machine.StationResultPublished += OnStationResultPublished;
        machine.StationResultProcessingFailed += OnStationResultProcessingFailed;
        machine.RunningStateChanged += OnMachineRunningStateChanged;
        this.productionContext.PropertyChanged += OnProductionContextPropertyChanged;
        this.rawInputBarcodeReceiver.BarcodeReceived += OnRawInputBarcodeReceived;
        this.mesConnection.StateChanged += OnMesConnectionStateChanged;
        this.localizationService.LanguageChanged += OnLanguageChanged;
        stationLimitsAppliedSubscription = messageBus.Subscribe<HomeViewModel, StationLimitsAppliedMessage>(
            this,
            static (viewModel, _) => viewModel.OnStationLimitsApplied());
        localWorkOrderRecipeLoadedSubscription = messageBus.Subscribe<HomeViewModel, LocalWorkOrderRecipeLoadedMessage>(
            this,
            static (viewModel, message) => _ = viewModel.ApplyLocalWorkOrderRecipeAsync(message),
            MessageSubscribeOptions<LocalWorkOrderRecipeLoadedMessage>.OnUI);
        _ = RefreshStationEnabledStatesForHomeAsync();
    }

    public MachineBase CurrentMachine => machine;

    public MesConnectionStatus MesStatus => mesConnectionStatus;

    public string MachineId => machine.MachineId;

    public string MachineName => machine.MachineName;

    public ObservableCollection<IDataGridColumnDescriptor> PartColumns => partColumns;

    public ObservableCollection<DisplayRowItem> PartRows => machine.PartRows;

    public uint ElectricalTestOkCount => (machine as IMachineProductionCountMachine)?.ElectricalTestOkCount ?? 0;

    public uint MaterialInputCount => (machine as IMachineProductionCountMachine)?.MaterialInputCount ?? 0;

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
        var successDialogCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (mesStateSyncRoot)
        {
            mesConnectSuccessDialogCompletion = successDialogCompletion;
        }

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
        finally
        {
            successDialogCompletion.TrySetResult();
            lock (mesStateSyncRoot)
            {
                if (ReferenceEquals(mesConnectSuccessDialogCompletion, successDialogCompletion))
                {
                    mesConnectSuccessDialogCompletion = null;
                }
            }
        }
    }
    
    private async Task DisconnectMesAsync()
    {
        if (!securityKeyChecker.IsPresent())
        {
            await ShowWarningOnUiAsync(localizationService.T("Home.Message.MesDisconnectNoKey", "No permission to disconnect MES."), localizationService.T("Home.Title.MesDisconnect", "MES Disconnect")).ConfigureAwait(false);
            return;
        }

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

    private IReadOnlyList<string> GetMissingStartupFields()
    {
        var missingFields = new List<string>();

        AddIfEmpty(TablePaperCode, "Home.Field.TablePaper", "台纸");
        AddIfEmpty(TopCoverCode, "Home.Field.TopCover", "上盖");
        if (ShowReelMatNo)
        {
            AddIfEmpty(ReelMatNo, "Home.Field.ReelMatNo", "Reel批号");
        }

        AddIfEmpty(OperatorNo, "Home.Field.Operator", "操作员");
        AddIfEmpty(EquipmentNo, "Home.Field.EquipmentNo", "机台号");
        AddIfEmpty(MachineType, "Home.Field.MachineType", "机种");
        return missingFields;

        void AddIfEmpty(string value, string localizationKey, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missingFields.Add(localizationService.T(localizationKey, fallback));
            }
        }
    }

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

            IReadOnlyList<string> missingStartupFields = GetMissingStartupFields();
            if (missingStartupFields.Count > 0)
            {
                await ShowWarningOnUiAsync(
                    localizationService.TF(
                        "Home.Message.StartRequiredFieldsMissing",
                        "请先完成以下信息扫描：{0}",
                        string.Join("、", missingStartupFields)),
                    localizationService.T("Home.Action.Start", "Start")).ConfigureAwait(false);
                return;
            }

            // 启动前始终按当前连接状态重新取得工单参数：在线取 MES，
            // 离线只取同工单号的本地 JSON，避免沿用上一张工单留在内存中的配置。
            string scannedTablePaperCode = TablePaperCode;
            string scannedTopCoverCode = TopCoverCode;
            string scannedMachineType = MachineType;
            bool isMesOnline = mesConnection.State == MesConnectionState.Online;
            if (isMesOnline
                && !await RefreshCurrentWorkOrderSetupFromMesAsync(
                    // 在线切换/扫码时已完成过 X 机种 Ls 下限输入的，启动前仅重新获取并下发参数；
                    // PreserveTrailingXMachineTypeLsLowerLimit 会保留该人工输入值。只有仍处于
                    // requiresLsLowerLimitOverride 状态时，下面的启动防呆才再次要求输入。
                    promptTrailingXLowerLimit: false,
                    previousMachineType: scannedMachineType).ConfigureAwait(false))
            {
                return;
            }

            if (!isMesOnline && offlineWorkOrderRescanRequired)
            {
                await ShowWarningOnUiAsync(
                    localizationService.T(
                        "Home.Message.OfflineWorkOrderRequired",
                        "MES 离线时请先扫描工单，再扫描机种加载本地参数。"),
                    localizationService.T("Home.Title.StartFailed", "启动失败")).ConfigureAwait(false);
                return;
            }

            if (!isMesOnline
                && !await RefreshCurrentWorkOrderSetupFromLocalRecipeAsync(scannedMachineType).ConfigureAwait(false))
            {
                return;
            }

            if (!await ValidateScannedMaterialsForCurrentWorkOrderAsync(
                scannedTablePaperCode,
                scannedTopCoverCode).ConfigureAwait(false))
            {
                return;
            }

            if (requiresLsLowerLimitOverride)
            {
                await ShowWarningOnUiAsync(
                    localizationService.T("Home.Message.XMachineLsLowerLimitRequired", "X机种必须手动输入Ls下限。"),
                    localizationService.T("Home.Title.LsLowerLimit", "Ls 下限")).ConfigureAwait(false);

                if (currentWorkOrderSetup != null)
                {
                    await ApplyTrailingXMachineTypeLsLowerLimitAsync(currentWorkOrderSetup).ConfigureAwait(false);
                }

                return;
            }

            if (!await ValidateCorrectionFrequencyAsync().ConfigureAwait(false))
            {
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

            if (!await ApplyCameraStartupOptionsAsync().ConfigureAwait(false))
            {
                return;
            }

            if (isMesOnline)
            {
                MesResult<MesTrackResult> trackInResult = await mesTrackService.TrackInAsync(
                    new MesTrackRequest(CreateMesContext(), GetCurrentUnitId(), WorkOrderNo),
                    DestroyToken).ConfigureAwait(false);

                if (!IsMesAccepted(trackInResult))
                {
                    await ShowErrorOnUiAsync(MesFailureMessageFormatter.Format(localizationService.T("Home.Title.MesTrackIn", "MES Track In"), trackInResult), localizationService.T("Home.Title.MesTrackIn", "MES Track In")).ConfigureAwait(false);
                    return;
                }
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

            bool isMesOnline = mesConnection.State == MesConnectionState.Online;
            bool shouldFinalize = await ShowConfirmOnUiAsync(
                isMesOnline
                    ? localizationService.T("Home.Message.TrackOutConfirm", "Track out?")
                    : localizationService.T("Home.Message.OfflineEndWorkOrderConfirm", "End the current work order?"),
                isMesOnline
                    ? localizationService.T("Home.Title.MesTrackOut", "MES Track Out")
                    : localizationService.T("Home.Title.OfflineEndWorkOrder", "End Work Order")).ConfigureAwait(false);

            await machine.StopAsync().ConfigureAwait(false);
            if (machine is IMachineWorkOrderStartSignalMachine workOrderStartSignalMachine)
            {
                await workOrderStartSignalMachine.ResetWorkOrderStartSignalsAsync(DestroyToken).ConfigureAwait(false);
            }

            if (!shouldFinalize)
            {
                return;
            }

            if (!await SaveProductionDataAsync(DestroyToken).ConfigureAwait(false))
            {
                return;
            }

            if (isMesOnline)
            {
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
                }
            }

            if (machine is IMachineProductionCounterResetMachine counterResetMachine)
            {
                await counterResetMachine.ResetProductionCounterAsync(DestroyToken).ConfigureAwait(false);
            }
            // 结束工单只清空 Home 的生产显示；标准件、确认件及点检结果
            // 是否保留仅由下一次实际加载到的机种是否变化决定。
            ClearForNewWorkOrderScan(clearSampleState: false);
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

            if (moved)
            {
                try
                {
                    await productionDataArchiveService.AppendProductionRecordAsync(
                        BuildProductionDataArchiveRequest(Path.Combine(outputDirectory, fileName)),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await ShowWarningOnUiAsync(
                        localizationService.TF("Home.Message.ProductionDataArchiveFailed", "本地生产记录保存失败：{0}", ex.Message),
                        localizationService.T("Home.Title.ProductionDataArchiveFailed", "本地生产记录保存失败")).ConfigureAwait(false);
                }
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

    private async Task<bool> ApplyCameraStartupOptionsAsync()
    {
        TestStationModel? cameraAStation = machine.TestStations.FirstOrDefault(station => station.StationId == 5 && station.IconKind == StationIconKind.Camera);
        TestStationModel? cameraBStation = machine.TestStations.FirstOrDefault(station => station.StationId == 6 && station.IconKind == StationIconKind.Camera);
        if (cameraAStation == null && cameraBStation == null)
        {
            return true;
        }

        CameraStartupOptions? options = securityKeyChecker.IsPresent()
            ? await InvokeOnUiAsync(() => cameraStartupOptionsDialogService.ShowAsync(
                cameraAStation?.IsEnabled ?? true,
                cameraBStation?.IsEnabled ?? true)).ConfigureAwait(false)
            : new CameraStartupOptions(true, true);
        if (options == null)
        {
            return false;
        }

        if (cameraAStation != null)
        {
            await machine.SetStationEnabledAsync(cameraAStation, options.IsCameraAEnabled, DestroyToken).ConfigureAwait(false);
        }

        if (cameraBStation != null)
        {
            await machine.SetStationEnabledAsync(cameraBStation, options.IsCameraBEnabled, DestroyToken).ConfigureAwait(false);
        }

        logService?.Info(localizationService.TF(
            "Home.Log.CameraStartupOptions",
            "Camera startup options applied. A={0}, B={1}",
            options.IsCameraAEnabled,
            options.IsCameraBEnabled));
        return true;
    }

    private ProductionDataArchiveRequest BuildProductionDataArchiveRequest(string sourceFilePath)
        => new(
            DateTimeOffset.Now,
            WorkOrderNo,
            OperatorNo,
            machine.MachineId,
            sourceFilePath,
            [
                BuildProductionMeasurementDefinition("Dcr", "DCR1", "DCR"),
                BuildProductionMeasurementDefinition("Ls", "Ls"),
                BuildProductionMeasurementDefinition("Rs", "Rs"),
                BuildProductionMeasurementDefinition("H_Ls", "Ls2", "H_Ls"),
                BuildProductionMeasurementDefinition("H_Q", "Q", "Q2", "H_Q")
            ]);

    private ProductionDataMeasurementDefinition BuildProductionMeasurementDefinition(string reportName, params string[] parameterIds)
    {
        StationMeasurementLimit? limit = machine.TestStations
            .SelectMany(station => parameterIds.Select(parameterId => station.TestLimits.TryGetValue(parameterId, out StationMeasurementLimit? value) ? value : null))
            .FirstOrDefault(value => value != null);
        StandardSampleLimitItemModel? standardItem = sampleState.StandardSample.LimitItems
            .FirstOrDefault(item => parameterIds.Any(parameterId => IsSameSampleLimit(item, parameterId)));
        bool enabled = limit != null;
        return new ProductionDataMeasurementDefinition(
            reportName,
            limit?.Unit ?? standardItem?.Unit ?? string.Empty,
            standardItem?.StandardValue ?? string.Empty,
            limit?.UpperLimit,
            limit?.LowerLimit,
            enabled);
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

        // Raw Input 使用后台接收，焦点位于原生弹窗时仍会收到键盘数据。
        // 弹窗中的输入只能由弹窗自身处理，不能再被误当成主页盲扫。
        if (HasActiveDialog())
        {
            logService?.Info(localizationService.TF(
                "Home.Log.BlindScanIgnoredDialogActive",
                "Blind scan ignored because a dialog is active: {0}",
                value));
            return;
        }

        if (!string.Equals(primaryNavigationState.CurrentView, ViewNames.HomeView, StringComparison.OrdinalIgnoreCase))
        {
            logService?.Info(localizationService.TF(
                "Home.Log.BlindScanIgnoredNonHome",
                "Blind scan ignored because the current page is {0}: {1}",
                primaryNavigationState.CurrentView,
                value));
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

    private static bool HasActiveDialog()
    {
        Window? mainWindow = Application.Current?.MainWindow;
        return Application.Current?.Windows
            .OfType<Window>()
            .Any(window => !ReferenceEquals(window, mainWindow) && window.IsVisible) == true;
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
                string previousMachineType = lastResolvedMachineType;
                string workOrderNo = CleanWorkOrderBarcode(value);
                ClearForNewWorkOrderScan(clearSampleState: false);
                await ResetProductionCounterForNewWorkOrderAsync().ConfigureAwait(false);
                WorkOrderNo = workOrderNo;
                if (mesConnection.State == MesConnectionState.Online)
                {
                    await LoadWorkOrderSetupAsync(workOrderNo, previousMachineType).ConfigureAwait(false);
                }
                else
                {
                    offlineWorkOrderRescanRequired = false;
                    // 离线时固定为“工单 → 机种”的扫码顺序：工单号只作为本次生产记录，
                    // 等机种条码到达后才按机种加载本地 JSON 并下发参数。
                    logService?.Info(localizationService.T(
                        "Home.Message.OfflineMachineTypeScanRequired",
                        "工单已扫描，请继续扫描机种以加载本地参数。"));
                }
                break;
            case 18:
                // 在线时机种只能由 MES 工单解析结果写入；离线时允许通过机种条码补录。
                if (mesConnection.State != MesConnectionState.Online)
                {
                    if (offlineWorkOrderRescanRequired || string.IsNullOrWhiteSpace(WorkOrderNo))
                    {
                        await ShowWarningOnUiAsync(
                            localizationService.T(
                                "Home.Message.OfflineWorkOrderRequired",
                                "MES 离线时请先扫描工单，再扫描机种加载本地参数。"),
                            localizationService.T("ParameterDict.Title.Load", "加载参数字典")).ConfigureAwait(false);
                        break;
                    }

                    string previousOfflineMachineType = lastResolvedMachineType;
                    string scannedMachineType = CleanRawBarcodeValue(value);
                    await HandleMachineTypeChangedAsync(previousOfflineMachineType, scannedMachineType).ConfigureAwait(false);

                    MachineType = scannedMachineType;
                    if (!string.IsNullOrWhiteSpace(scannedMachineType))
                    {
                        lastResolvedMachineType = scannedMachineType;
                    }
                    // 离线配方以机种为主键；工单号已经扫描后，扫到机种才加载并下发。
                    await LoadOfflineWorkOrderRecipeAsync(
                        WorkOrderNo,
                        previousOfflineMachineType,
                        scannedMachineType).ConfigureAwait(false);
                }
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
            ClearChartSamples();
            ClearChartLimits();
            SyncTapeParameterRows(null);
            if (clearSampleState)
            {
                ClearSampleState();
            }
        });

    private async Task LoadWorkOrderSetupAsync(
        string workOrderNo,
        string? previousMachineType = null)
    {
        if (mesConnection.State != MesConnectionState.Online)
        {
            await LoadOfflineWorkOrderRecipeAsync(workOrderNo, previousMachineType, machineTypeOverride: null).ConfigureAwait(false);
            return;
        }

        // 在线工单只以 MES 数据为准；不能让上一次离线编辑会话在下次断线时
        // 重新覆盖刚从 MES 刷新的编带、编带字符等运行时参数。
        localWorkOrderRecipeSession.Clear();

        MesResult<MesWorkOrderSetup> result = await mesWorkOrderService.GetWorkOrderSetupAsync(
            new MesWorkOrderRequest(CreateMesContext(), workOrderNo),
            DestroyToken).ConfigureAwait(false);

        if (!IsMesAccepted(result) || result.Data == null)
        {
            await ShowErrorOnUiAsync(MesFailureMessageFormatter.Format(localizationService.TF("Home.Message.WorkOrderImportWithNo", "Work order {0} import", workOrderNo), result), localizationService.T("Home.Title.WorkOrderImport", "Work Order Import")).ConfigureAwait(false);
            return;
        }

        await ApplyWorkOrderSetupToMachineAsync(
            result.Data,
            previousMachineType,
            workOrderNo,
            showImportSuccessMessage: true,
            applyTrailingXOverride: true).ConfigureAwait(false);
    }

    /// <summary>
    /// 离线盲扫按机种读取本地 JSON；工单号只用于本次生产记录，不再作为文件主键。
    /// 下发仍复用在线工单的运行时配置与硬件写入两阶段链路。
    /// </summary>
    private async Task LoadOfflineWorkOrderRecipeAsync(
        string workOrderNo,
        string? previousMachineType,
        string? machineTypeOverride)
    {
        string machineType = LocalWorkOrderRecipeStore.NormalizeMachineType(machineTypeOverride ?? MachineType);
        if (string.IsNullOrWhiteSpace(machineType))
        {
            logService?.Info(localizationService.T(
                "Home.Message.OfflineMachineTypeScanRequired",
                "工单已扫描，请继续扫描机种以加载本地参数。"));
            return;
        }

        LocalWorkOrderRecipe? recipe = localWorkOrderRecipeStore.Load(machineType);
        if (recipe == null
            || string.IsNullOrWhiteSpace(recipe.EquipmentType)
            || !string.Equals(recipe.EquipmentType, machineType, StringComparison.OrdinalIgnoreCase))
        {
            await ShowErrorOnUiAsync(
                localizationService.TF(
                    "Home.Message.OfflineMachineRecipeNotFound",
                    "离线机种配方 {0} 不存在，请先在参数字典中创建或导入。",
                    machineType),
                localizationService.T("ParameterDict.Title.Load", "加载参数字典")).ConfigureAwait(false);
            return;
        }

        // 盲扫与参数字典手动加载必须使用同一份“实际文件路径”会话信息，
        // 后续在 SetView 应用工位参数时才能写回该 JSON。
        localWorkOrderRecipeSession.SetCurrent(recipe, localWorkOrderRecipeStore.GetFilePath(machineType));
        MesWorkOrderSetup setup = localWorkOrderRecipeMapper.ToMesSetup(recipe) with
        {
            WorkOrderNo = workOrderNo,
            EquipmentType = machineType
        };

        await ApplyWorkOrderSetupToMachineAsync(
            setup,
            previousMachineType,
            workOrderNo,
            showImportSuccessMessage: false,
            // 离线时机种可由 18 位条码补录；工单在其之前或之后扫描时，
            // 都必须执行同一套 X 机种 Ls 下限输入防呆。
            applyTrailingXOverride: true).ConfigureAwait(false);

        await CaptureAndSaveRecipeInstrumentConfigsAsync(recipe).ConfigureAwait(false);

        RunOnUi(() => messageBus.Publish(new LocalWorkOrderRecipeAppliedMessage(recipe)));
        await ShowMessageOnUiAsync(
            localizationService.TF("ParameterDict.Message.LoadSucceeded", "本地机种配方 {0} 已加载。", machineType),
            localizationService.T("ParameterDict.Title.Load", "加载参数字典")).ConfigureAwait(false);
    }

    private async Task ApplyLocalWorkOrderRecipeAsync(LocalWorkOrderRecipeLoadedMessage message)
    {
        if (mesConnectionStatus.State == MesConnectionState.Online)
        {
            await ShowWarningOnUiAsync(
                localizationService.T("ParameterDict.Message.LoadMesOnlineBlocked", "MES 在线时禁止加载本地工单配方，请先断开 MES。"),
                localizationService.T("ParameterDict.Title.Load", "加载参数字典")).ConfigureAwait(false);
            return;
        }

        string previousMachineType = lastResolvedMachineType;
        localWorkOrderRecipeSession.SetCurrent(message.Recipe, message.FilePath);
        ClearForNewWorkOrderScan(clearSampleState: false);
        await ResetProductionCounterForNewWorkOrderAsync().ConfigureAwait(false);
        if (message.PrepareForEditing)
        {
            // A newly created recipe has no measurements yet.  Do not attempt
            // to write an incomplete configuration to hardware; SetView now
            // edits the current device configurations and persists each apply
            // into this recipe.
            currentWorkOrderSetup = message.Setup;
            areStationLimitsVisible = true;
            RunOnUi(() => messageBus.Publish(new LocalWorkOrderRecipeAppliedMessage(message.Recipe)));
            await ShowMessageOnUiAsync(
                localizationService.TF("ParameterDict.Message.NewEditReady", "本地机种配方 {0} 已创建，请在工位参数中编辑后点击应用保存。", message.Recipe.EquipmentType),
                localizationService.T("ParameterDict.Title.New", "新建参数字典")).ConfigureAwait(false);
            return;
        }

        await ApplyWorkOrderSetupToMachineAsync(
            message.Setup,
            previousMachineType,
            WorkOrderNo,
            showImportSuccessMessage: false,
            applyTrailingXOverride: false).ConfigureAwait(false);
        await CaptureAndSaveRecipeInstrumentConfigsAsync(message.Recipe).ConfigureAwait(false);
        // 参数已经下发完成后立即通知 SetView 重绑当前工位。成功提示是模态框，
        // 不能让它阻塞工位参数页的刷新与跳转。
        RunOnUi(() => messageBus.Publish(new LocalWorkOrderRecipeAppliedMessage(message.Recipe)));
        await ShowMessageOnUiAsync(
            message.ShowImportSuccess
                ? localizationService.TF("ParameterDict.Message.ImportSucceeded", "机种参数已导入到本地机种配方 {0}。", message.Recipe.EquipmentType)
                : localizationService.TF("ParameterDict.Message.LoadSucceeded", "本地机种配方 {0} 已加载。", message.Recipe.EquipmentType),
            message.ShowImportSuccess
                ? localizationService.T("ParameterDict.Title.Import", "导入工单参数")
                : localizationService.T("ParameterDict.Title.Load", "加载参数字典")).ConfigureAwait(false);
    }

    private Task CaptureAndSaveRecipeInstrumentConfigsAsync(LocalWorkOrderRecipe recipe)
    {
        localWorkOrderRecipeMapper.CaptureInstrumentConfigs(
            recipe,
            devices.Devices,
            machine.TestStations.SelectMany(static station => station.InstrumentDeviceIds));
        localWorkOrderRecipeMapper.RemoveRedundantRecipeData(recipe);
        return localWorkOrderRecipeStore.SaveAsync(recipe, localWorkOrderRecipeSession.CurrentFilePath);
    }

    private async Task ApplyWorkOrderSetupToMachineAsync(
        MesWorkOrderSetup setup,
        string? previousMachineType,
        string workOrderNo,
        bool showImportSuccessMessage,
        bool applyTrailingXOverride)
    {
        setup = PreserveTrailingXMachineTypeLsLowerLimit(setup);
        currentWorkOrderSetup = setup;
        areStationLimitsVisible = true;
        // Phase 1 must always finish first: pages bind to these live configurations,
        // even when a physical device or PLC is temporarily unavailable.
        await machine.ApplyWorkOrderRuntimeSetupAsync(setup, DestroyToken).ConfigureAwait(false);
        await SaveBraidOptionsAsync(setup.TapeSetup).ConfigureAwait(false);
        await SaveMarkPrintOptionsAsync(setup).ConfigureAwait(false);
        RunOnUi(() =>
        {
            if (machine.RefreshResultGridIfStructureChanged())
            {
                SyncColumns();
                SyncChartTabs();
            }
        });
        string newMachineType = GetWorkOrderMachineType(setup);
        await HandleMachineTypeChangedAsync(previousMachineType, newMachineType).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(newMachineType))
        {
            lastResolvedMachineType = newMachineType;
        }
        ApplyWorkOrderSetup(setup);
        productionContext.IsResultGridDataEnabled = true;
        SyncChartLimits();
        SyncTapeParameterRows(setup.TapeSetup);

        WorkOrderHardwareWriteResult hardwareWriteResult = await machine
            .WriteWorkOrderSetupToHardwareAsync(setup, DestroyToken)
            .ConfigureAwait(false);
        if (!hardwareWriteResult.IsSuccess)
        {
            string details = string.Join(Environment.NewLine, hardwareWriteResult.Failures.Select(item => $"{item.Target}: {item.Message}"));
            await ShowWarningOnUiAsync(
                localizationService.TF("Home.Message.WorkOrderHardwareWriteFailed", "Work-order parameters have refreshed, but one or more device writes failed:{0}{1}", Environment.NewLine, details),
                localizationService.T("Home.Title.WorkOrderHardwareWriteFailed", "Parameter Write Warning")).ConfigureAwait(false);
        }

        if (showImportSuccessMessage)
        {
            await ShowMessageOnUiAsync(localizationService.TF("Home.Message.WorkOrderImportSuccess", "Work order {0} imported.", workOrderNo), localizationService.T("Home.Title.WorkOrderImport", "Work Order Import")).ConfigureAwait(false);
        }

        if (applyTrailingXOverride && !HasConfirmedTrailingXMachineLsLowerLimit(newMachineType))
        {
            await ApplyTrailingXMachineTypeLsLowerLimitAsync(setup).ConfigureAwait(false);
        }

        // The work-order setup has updated live device configuration.  Notify all
        // parameter consumers after optional X-type overrides are complete, so an
        // already opened SetView station editor rebinds without a tab switch.
        messageBus.Publish(new StationLimitsAppliedMessage());
    }

    private async void OnMesConnectionStateChanged(object? sender, KwyTemplate.MES.Abstract.Events.MesStateChangedEventArgs e)
    {
        bool shouldRefresh;
        bool shouldRequireOfflineWorkOrderRescan;
        Task? waitForSuccessDialog;
        lock (mesStateSyncRoot)
        {
            shouldRefresh = lastMesConnectionState != MesConnectionState.Online
                && e.State == MesConnectionState.Online;
            shouldRequireOfflineWorkOrderRescan = lastMesConnectionState == MesConnectionState.Online
                && e.State != MesConnectionState.Online;
            waitForSuccessDialog = shouldRefresh ? mesConnectSuccessDialogCompletion?.Task : null;
            lastMesConnectionState = e.State;
        }

        if (shouldRefresh)
        {
            // MES 恢复在线后结束离线配方编辑会话。运行时参数已经由 MES 刷新，
            // 后续单纯断开 MES 时仍保持这些值；只有再次扫描本地机种才重新进入离线配方。
            localWorkOrderRecipeSession.Clear();
        }

        if (shouldRequireOfflineWorkOrderRescan)
        {
            // 点击断开 MES 不改变当前 Home 的展示和运行参数；只使下一次离线生产
            // 必须重新执行“工单 → 机种”扫码，之后才加载本地 JSON。
            offlineWorkOrderRescanRequired = true;
            localWorkOrderRecipeSession.Clear();
        }

        if (!shouldRefresh || string.IsNullOrWhiteSpace(WorkOrderNo)
            || Interlocked.Exchange(ref onlineWorkOrderRefreshPending, 1) != 0)
        {
            return;
        }

        // 先结束旧的离线编辑会话。DisconnectAsync 内部可能同步触发状态变更，
        // 若等到状态事件才清理，SetView 第二次进入编带/编带字符页仍可能读到旧 JSON。
        // 当前在线参数只保留在运行时；下一次离线必须重新扫码后才加载本地机种配方。
        offlineWorkOrderRescanRequired = true;
        localWorkOrderRecipeSession.Clear();

        try
        {
            if (waitForSuccessDialog != null)
            {
                await waitForSuccessDialog.ConfigureAwait(false);
            }

            await RefreshCurrentWorkOrderSetupFromMesAsync(promptTrailingXLowerLimit: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await ShowErrorOnUiAsync(
                ex.Message,
                localizationService.T("Home.Title.WorkOrderImport", "Work Order Import")).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref onlineWorkOrderRefreshPending, 0);
        }
    }

    private async Task<bool> RefreshCurrentWorkOrderSetupFromMesAsync(
        bool promptTrailingXLowerLimit = false,
        string? previousMachineType = null)
    {
        string workOrderNo = WorkOrderNo.Trim();
        if (string.IsNullOrWhiteSpace(workOrderNo))
        {
            return false;
        }

        MesResult<MesWorkOrderSetup> result = await mesWorkOrderService.GetWorkOrderSetupAsync(
            new MesWorkOrderRequest(CreateMesContext(), workOrderNo),
            DestroyToken).ConfigureAwait(false);

        if (!IsMesAccepted(result) || result.Data == null)
        {
            await ShowErrorOnUiAsync(
                MesFailureMessageFormatter.Format(localizationService.TF("Home.Message.WorkOrderImportWithNo", "Work order {0} import", workOrderNo), result),
                localizationService.T("Home.Title.WorkOrderImport", "Work Order Import")).ConfigureAwait(false);
            return false;
        }

        MesWorkOrderSetup setup = PreserveTrailingXMachineTypeLsLowerLimit(result.Data);
        await ApplyWorkOrderSetupToMachineAsync(
            setup,
            previousMachineType ?? lastResolvedMachineType,
            workOrderNo,
            showImportSuccessMessage: false,
            // 仅在 MES 从离线恢复在线时强制提示；启动前的工单刷新只复用
            // 已成功输入的下限，不能再次弹窗干扰启动流程。
            applyTrailingXOverride: promptTrailingXLowerLimit).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 离线启动前按当前机种强制重新读取本地配方，并复用在线工单的完整应用链路。
    /// 本地文件缺失时不允许启动，避免错误沿用上一机种的内存参数。
    /// </summary>
    private async Task<bool> RefreshCurrentWorkOrderSetupFromLocalRecipeAsync(string? previousMachineType)
    {
        string workOrderNo = WorkOrderNo.Trim();
        string machineType = LocalWorkOrderRecipeStore.NormalizeMachineType(MachineType);
        LocalWorkOrderRecipe? recipe = string.IsNullOrWhiteSpace(machineType)
            ? null
            : localWorkOrderRecipeStore.Load(machineType);
        if (recipe == null
            || string.IsNullOrWhiteSpace(recipe.EquipmentType)
            || !string.Equals(recipe.EquipmentType, machineType, StringComparison.OrdinalIgnoreCase))
        {
            await ShowErrorOnUiAsync(
                localizationService.TF(
                    "Home.Message.OfflineMachineRecipeNotFound",
                    "离线机种配方 {0} 不存在，请先在参数字典中创建或导入。",
                    machineType),
                localizationService.T("Home.Title.StartFailed", "启动失败")).ConfigureAwait(false);
            return false;
        }

        localWorkOrderRecipeSession.SetCurrent(recipe, localWorkOrderRecipeStore.GetFilePath(machineType));
        MesWorkOrderSetup setup = localWorkOrderRecipeMapper.ToMesSetup(recipe) with { WorkOrderNo = workOrderNo, EquipmentType = machineType };
        await ApplyWorkOrderSetupToMachineAsync(
            setup,
            previousMachineType ?? lastResolvedMachineType,
            workOrderNo,
            showImportSuccessMessage: false,
            applyTrailingXOverride: false).ConfigureAwait(false);
        await CaptureAndSaveRecipeInstrumentConfigsAsync(recipe).ConfigureAwait(false);
        messageBus.Publish(new LocalWorkOrderRecipeAppliedMessage(recipe));

        return true;
    }

    private async Task<bool> ValidateScannedMaterialsForCurrentWorkOrderAsync(
        string scannedTablePaperCode,
        string scannedTopCoverCode)
    {
        MesWorkOrderMaterialRequirements? requirements = currentWorkOrderSetup?.MaterialRequirements;
        bool isTablePaperMatched = requirements != null
            && MaterialNoMatches(scannedTablePaperCode, requirements.TablePaperMatNo);
        bool isTopCoverMatched = requirements != null
            && MaterialNoMatches(scannedTopCoverCode, requirements.TopCoverMatNo);
        if (isTablePaperMatched && isTopCoverMatched)
        {
            return true;
        }

        RunOnUi(() =>
        {
            TablePaperCode = string.Empty;
            TopCoverCode = string.Empty;
        });
        await ShowWarningOnUiAsync(
            localizationService.T(
                "Home.Message.StartMaterialMismatch",
                "台纸或上盖与当前工单不一致，已清空，请重新扫描。"),
            localizationService.T("Home.Title.MaterialCheck", "物料校验")).ConfigureAwait(false);
        return false;
    }

    private MesWorkOrderSetup PreserveTrailingXMachineTypeLsLowerLimit(MesWorkOrderSetup refreshedSetup)
    {
        string machineType = GetWorkOrderMachineType(refreshedSetup);
        if (!HasConfirmedTrailingXMachineLsLowerLimit(machineType)
            || requiresLsLowerLimitOverride
            || currentWorkOrderSetup == null)
        {
            return refreshedSetup;
        }

        MesWorkOrderInstrumentSetup? currentLsSetup = currentWorkOrderSetup.InstrumentSetups?.FirstOrDefault(item =>
            string.Equals(item.ParameterId, "Ls", StringComparison.OrdinalIgnoreCase)
            && item.LowerLimit.HasValue);
        if (currentLsSetup?.LowerLimit is not double currentLowerLimit)
        {
            return refreshedSetup;
        }

        MesWorkOrderInstrumentSetup[] instrumentSetups = (refreshedSetup.InstrumentSetups ?? [])
            .Select(item => string.Equals(item.ParameterId, "Ls", StringComparison.OrdinalIgnoreCase)
                ? item with { LowerLimit = currentLowerLimit }
                : item)
            .ToArray();

        return refreshedSetup with { InstrumentSetups = instrumentSetups };
    }

    private async Task ApplyTrailingXMachineTypeLsLowerLimitAsync(MesWorkOrderSetup setup)
    {
        requiresLsLowerLimitOverride = false;
        // 在线工单以本次 MES setup 的机种为准；离线允许 18 位条码覆盖机种。
        // 不依赖 UI 属性已完成刷新，避免在线导入时漏掉 X 机种防呆。
        string displayedMachineType = MachineType.Trim();
        string parsedMachineType = GetWorkOrderMachineType(setup);
        bool isTrailingXMachineType = displayedMachineType.EndsWith("X", StringComparison.OrdinalIgnoreCase)
            || parsedMachineType.EndsWith("X", StringComparison.OrdinalIgnoreCase);
        if (!isTrailingXMachineType)
        {
            confirmedTrailingXMachineType = string.Empty;
            SpecialMachineLsLowerLimitText = string.Empty;
            SpecialMachineLsUnit = string.Empty;
            return;
        }

        if (HasConfirmedTrailingXMachineLsLowerLimit(parsedMachineType))
        {
            return;
        }

        logService?.Info(localizationService.TF(
            "Home.Log.TrailingXMachineType",
            "Trailing-X machine type detected. DisplayedMachineType={0}, ParsedMachineType={1}",
            displayedMachineType,
            parsedMachineType));

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

        await machine.ApplyWorkOrderRuntimeSetupAsync(overriddenSetup, DestroyToken).ConfigureAwait(false);
        currentWorkOrderSetup = overriddenSetup;
        confirmedTrailingXMachineType = parsedMachineType;
        SpecialMachineLsLowerLimitText = lowerLimit.ToString("G29", CultureInfo.CurrentCulture);
        SpecialMachineLsUnit = lsSetup.Unit?.Trim() ?? string.Empty;
        RunOnUi(() =>
        {
            if (machine.RefreshResultGridIfStructureChanged())
            {
                SyncColumns();
                SyncChartTabs();
            }
        });
        SyncChartLimits();

        WorkOrderHardwareWriteResult hardwareWriteResult = await machine
            .WriteWorkOrderSetupToHardwareAsync(overriddenSetup, DestroyToken)
            .ConfigureAwait(false);
        if (!hardwareWriteResult.IsSuccess)
        {
            string details = string.Join(Environment.NewLine, hardwareWriteResult.Failures.Select(item => $"{item.Target}: {item.Message}"));
            await ShowWarningOnUiAsync(
                localizationService.TF("Home.Message.WorkOrderHardwareWriteFailed", "Work-order parameters have refreshed, but one or more device writes failed:{0}{1}", Environment.NewLine, details),
                localizationService.T("Home.Title.WorkOrderHardwareWriteFailed", "Parameter Write Warning")).ConfigureAwait(false);
        }
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
        setup.Parameters.TryGetString("MarkPrintString", out string printString);
        await markPrintOptionsStore.SaveAsync(new MarkPrintOptions
        {
            PrintString = printString
        }).ConfigureAwait(false);

        await ApplyMarkPrintStringAsync(printString).ConfigureAwait(false);
    }

    private async Task ApplyMarkPrintStringAsync(string? printString)
    {
        if (machine is not IMachineMarkPrintOptionsMachine markPrintMachine)
        {
            return;
        }

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
        string materialNo = CleanMaterialBarcode(value);
        if (string.IsNullOrWhiteSpace(materialNo))
        {
            return;
        }

        // 在线、离线均使用当前已应用的工单/本地机种配方中的物料要求匹配。
        // 离线参数已在扫描机种时加载到 currentWorkOrderSetup，不能再按扫码次数猜测台纸或上盖。
        MesWorkOrderMaterialRequirements? requirements = currentWorkOrderSetup?.MaterialRequirements;
        if (requirements == null)
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
        return NormalizeMaterialNo(parts.Length > 0 ? parts[0] : value);
    }

    private static bool MaterialNoMatches(string materialNo, string? expectedMaterialNos)
    {
        if (string.IsNullOrWhiteSpace(expectedMaterialNos))
        {
            return false;
        }

        string actual = NormalizeMaterialNo(materialNo);
        return expectedMaterialNos
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeMaterialNo)
            .Any(expected => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMaterialNo(string? value)
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

    /// <summary>
    /// 校正页与 SetView 不各自维护频率：两者都从当前校正仪表配置和标准件状态派生。
    /// 启动前按校正页相同的优先级重建其显示频率，再和即将下发的仪表频率比较。
    /// </summary>
    private async Task<bool> ValidateCorrectionFrequencyAsync()
    {
        object? instrumentConfig = GetCorrectionInstrumentConfig();
        if (instrumentConfig == null)
        {
            return true;
        }

        CorrectionParameterSnapshot correction = correctionParameterProvider.CreateSnapshot(
            instrumentConfig,
            preferInstrumentFrequency: mesConnection.State != MesConnectionState.Online);

        if (!TryGetInstrumentFrequency(instrumentConfig, out double instrumentFrequency, out string instrumentFrequencyUnit)
            || !TryConvertFrequencyToHz(correction.Frequency, correction.FrequencyUnit, out double correctionFrequency)
            || !TryConvertFrequencyToHz(instrumentFrequency, instrumentFrequencyUnit, out double setFrequency))
        {
            // 没有可比较的频率时保留原有启动流程；不会因空的可选频率误拦截生产。
            return true;
        }

        if (Math.Abs(correctionFrequency - setFrequency) <= Math.Max(1e-9, Math.Abs(setFrequency) * 1e-9))
        {
            return true;
        }

        await ShowWarningOnUiAsync(
            localizationService.TF(
                "Home.Message.CorrectionFrequencyMismatch",
                "电感设定频率（{0} {1}）与校正频率（{2} {3}）不一致，请确认后再启动。",
                instrumentFrequency.ToString("0.##########", CultureInfo.InvariantCulture),
                instrumentFrequencyUnit,
                correction.Frequency,
                correction.FrequencyUnit),
            localizationService.T("Home.Title.StartFailed", "启动失败")).ConfigureAwait(false);
        return false;
    }

    private object? GetCorrectionInstrumentConfig()
    {
        foreach (TestStationModel station in machine.TestStations.Where(static station => station.Operations.Any(static operation =>
                     string.Equals(operation.Code, StationOperationDescriptor.Calibration, StringComparison.OrdinalIgnoreCase))))
        {
            foreach (string deviceId in station.InstrumentDeviceIds.Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                if (devices.TryGet(deviceId, out IInstrumentCorrection? instrument) && instrument != null)
                {
                    return instrument.DeviceParameter;
                }
            }
        }

        return null;
    }

    private static bool TryGetInstrumentFrequency(object config, out double value, out string unit)
    {
        object? rawValue = config.GetType().GetProperty("Frequency")?.GetValue(config);
        unit = config.GetType().GetProperty("FrequencyUnit")?.GetValue(config)?.ToString()?.Trim() ?? string.Empty;
        value = 0;
        return rawValue != null
            && double.TryParse(rawValue.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !string.IsNullOrWhiteSpace(unit);
    }

    private static bool TryConvertFrequencyToHz(string? value, string? unit, out double hertz)
    {
        hertz = 0;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            && TryConvertFrequencyToHz(number, unit, out hertz);
    }

    private static bool TryConvertFrequencyToHz(double value, string? unit, out double hertz)
    {
        hertz = value;
        switch (unit?.Trim().ToUpperInvariant())
        {
            case "HZ":
                return true;
            case "KHZ":
                hertz *= 1_000;
                return true;
            case "MHZ":
                hertz *= 1_000_000;
                return true;
            default:
                return false;
        }
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

    private async Task HandleMachineTypeChangedAsync(string? previousMachineType, string newMachineType)
    {
        if (!ShouldClearSampleStateForMachineTypeChange(previousMachineType, newMachineType))
        {
            return;
        }

        RunOnUi(ClearSampleState);
        confirmedTrailingXMachineType = string.Empty;
        requiresLsLowerLimitOverride = false;
        SpecialMachineLsLowerLimitText = string.Empty;
        SpecialMachineLsUnit = string.Empty;
        await machine.SetCheckCompletedAsync(false, DestroyToken).ConfigureAwait(false);
    }

    private bool HasConfirmedTrailingXMachineLsLowerLimit(string? machineType)
        => !string.IsNullOrWhiteSpace(machineType)
            && machineType.EndsWith("X", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(SpecialMachineLsLowerLimitText)
            && string.Equals(confirmedTrailingXMachineType, machineType, StringComparison.OrdinalIgnoreCase);

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

    private void OnBraidOptionsChanged(object? sender, EventArgs e)
        => RunOnUi(() => SyncTapeParameterRows(braidOptionsStore.Current.ToTapeSetup()));

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
            tapeParameterColumns.Add(new WpfDataGridColumnOptions
            {
                // “后不封膜”是 BlankQty 的第二个业务展示及 PLC 去向，
                // 使用独立列标识，但直接绑定唯一的数据源。
                Key = "BackNoFilmQty",
                Header = localizationService.T("Braid.BackNoFilmQty", "Back No Film"),
                BindingPath = nameof(TapeParameterRowModel.BlankQty),
                ElementStyleKey = "TapeParameterCellTextBlockStyle",
                CanUserSort = false,
                CanUserResize = false,
                CanUserReorder = false
            });
        });

    private static IDataGridColumnDescriptor CreateTapeParameterColumn(string bindingPath, string displayName)
        => new WpfDataGridColumnOptions
        {
            Key = bindingPath,
            Header = displayName,
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
                BlankQty = FormatNullableInt(tapeSetup.BlankQty)
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
    {
        PostOnUi(() =>
        {
            RaisePropertyChanged(nameof(ElectricalTestOkCount));
            RaisePropertyChanged(nameof(MaterialInputCount));
        });
        RequestChartLimitsSync();
    }

    private void OnStationResultPublished(object? sender, StationResultPublishedEventArgs e)
        => PostOnUi(() =>
        {
            if (e.ResultGeneration == machine.CurrentResultGeneration)
            {
                PushChartSamples(e);
            }
        }, DispatcherPriority.Render);

    private void OnStationResultProcessingFailed(object? sender, StationResultProcessingFailedEventArgs e)
        => logService?.Error(localizationService.TF(
            "Home.Log.StationResultProcessingFailed",
            "生产结果处理失败。工位：{0}，错误：{1}",
            e.Station.StationName,
            e.Exception.Message));

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

    private void ClearChartSamples()
        => RunOnUi(() =>
        {
            chartSampleSequence = 0;
            foreach (HomeChartTabModel tab in chartTabs)
            {
                tab.ClearSamples();
            }
        });

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
            machine.StationResultProcessingFailed -= OnStationResultProcessingFailed;
            machine.RunningStateChanged -= OnMachineRunningStateChanged;
            productionContext.PropertyChanged -= OnProductionContextPropertyChanged;
            rawInputBarcodeReceiver.BarcodeReceived -= OnRawInputBarcodeReceived;
            mesConnection.StateChanged -= OnMesConnectionStateChanged;
            localizationService.LanguageChanged -= OnLanguageChanged;
            braidOptionsStore.OptionsChanged -= OnBraidOptionsChanged;
            sampleState.StandardSample.LimitItems.CollectionChanged -= OnStandardSampleLimitItemsChanged;
            DetachStandardSampleLimitItemHandlers(sampleState.StandardSample.LimitItems);
            stationLimitsAppliedSubscription.Dispose();
            localWorkOrderRecipeLoadedSubscription.Dispose();
        }

        base.Dispose(disposing);
    }
}








