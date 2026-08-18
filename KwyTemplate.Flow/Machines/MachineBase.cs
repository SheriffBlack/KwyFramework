using System.Globalization;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions;
using Kwy.Device.Abstractions.IO;
using Kwy.Device.Abstractions.Instrument;
using Kwy.Device.Abstractions.PLC;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.Device.Devices;
using KwyTemplate.Flow.Common;
using KwyTemplate.Flow.DataDeals;
using Kwy.UI.DataGrids;
using KwyTemplate.Flow.Models;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 机台基类，统一管理设备绑定、工位调度、IO 快照、PLC 点位和生产数据推送。
/// 子类只需补充具体机型的设备、点位和特殊业务逻辑。
/// </summary>
public abstract class MachineBase : IMachine, IMachineResultProvider, IStationOperationMachine
{
    private const int MaxIoChannelCount = 64;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<int, CompositeDataDeal> orchestrators = new();
    private readonly List<Task> stationLifecycleTasks = [];
    private StationResultDispatchQueue? stationResultDispatchQueue;
    private Task? stationResultDispatchTask;
    private readonly Dictionary<string, MachinePlcPointDefinition> plcPointMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DisplayRowItem> partRowMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILocalizationService? localizationService;
    private readonly object ioSnapshotSync = new();
    private CancellationTokenSource? runningCts;
    private Task? machinePollingTask;
    private Task? ioSnapshotTask;
    private ulong currentDiMask;
    private ulong previousDiMask;
    private ulong latchedRisingDiMask;
    private ulong latchedFallingDiMask;
    private bool isRunning;
    private MachineProductionState productionState = MachineProductionState.Stopped;

    private volatile bool runtimeStopping;
    private bool disposed;

    protected MachineBase(IMachineDeviceContext devices, ILocalizationService? localizationService = null)
    {
        Devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.localizationService = localizationService;
        if (this.localizationService != null)
        {
            this.localizationService.LanguageChanged += OnLanguageChanged;
        }
    }

    protected readonly Dictionary<int, string> PlcAddressCache = new();

    protected IMachineDeviceContext Devices { get; }

    protected IPlcDevice? Plc { get; private set; }

    protected IIoCardDevice? IoCard { get; private set; }

    public virtual string MachineId => GetType().Name;

    public virtual string MachineName => GetType().Name;

    protected static IReadOnlySet<HomeWorkOrderField> CreateDefaultHomeWorkOrderFields()
        => new HashSet<HomeWorkOrderField>
        {
            HomeWorkOrderField.WorkOrderNo,
            HomeWorkOrderField.TablePaperCode,
            HomeWorkOrderField.TopCoverCode,
            HomeWorkOrderField.OperatorNo,
            HomeWorkOrderField.EquipmentNo,
            HomeWorkOrderField.MachineType,
            HomeWorkOrderField.BarcodeContent,
            HomeWorkOrderField.ReelTpNo,
            HomeWorkOrderField.ReelWorkOrderNo,
            HomeWorkOrderField.ReelId
        };

    public virtual IReadOnlySet<HomeWorkOrderField> HomeWorkOrderFields { get; } = CreateDefaultHomeWorkOrderFields();

    public virtual int MachinePollingIntervalMs { get; set; } = 1;

    public virtual int IoSnapshotPollingIntervalMs { get; set; } = 1;

    public virtual int SystemDataPollingIntervalMs { get; set; } = 100;

    public abstract TriggerMode StationTriggerMode { get; }

    public List<TestStationModel> TestStations { get; protected set; } = [];

    public IReadOnlyList<TestStationModel> Stations => TestStations;

    public ObservableCollection<DataGridColumnDescriptor> PartColumns { get; } = [];

    public ObservableCollection<DisplayRowItem> PartRows { get; } = [];

    IReadOnlyCollection<DataGridColumnDescriptor> IMachineResultProvider.PartColumns => PartColumns;

    IReadOnlyCollection<DisplayRowItem> IMachineResultProvider.PartRows => PartRows;

    public ObservableCollection<MachinePlcPointDefinition> PlcPointDefinitions { get; } = [];

    public bool IsRunning => isRunning;

    public MachineProductionState ProductionState => productionState;

    public event EventHandler? RunningStateChanged;

    /// <summary>
    /// 外部人机通过参数对比请求启动时通知应用层。仅用于提示等非阻断业务。
    /// </summary>
    public event Func<CancellationToken, Task>? ExternalStartRequested;

    public event EventHandler? TableChanged;

    public event EventHandler<StationResultPublishedEventArgs>? StationResultPublished;

    public event EventHandler<StationEnabledChangedEventArgs>? StationEnabledChanged;

    public abstract void InitTestStations();

    public void BindPlc(IPlcDevice? plc)
    {
        Plc = plc;
        if (plc == null)
        {
            return;
        }

        foreach (MachinePlcPointDefinition point in PlcPointDefinitions)
        {
            plc.RegisterPoint(point.Address, point.DisplayName, point.DataType, point.IsReadOnly);
        }
    }


    protected static string GetDescription(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?.Description ?? value.ToString();
    }

    protected void BindIoCard(IIoCardDevice? card)
    {
        IoCard = card;
        ResetIoSnapshot();
    }

    /// <summary>
    /// 绑定当前机型所需的设备实例。
    /// 设备由 KwyTemplate.Device 创建和统一连接，机台只拿已注册的实例使用。
    /// </summary>
    public virtual void BindDevices()
    {
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await StartRuntimeAsync(cancellationToken).ConfigureAwait(false);
        if (isRunning)
        {
            return;
        }

        await OnTestStartedAsync(cancellationToken).ConfigureAwait(false);
        isRunning = true;
        productionState = MachineProductionState.Running;
        RaiseRunningStateChanged();
    }

    public Task StopAsync()
        => StopProductionCoreAsync(OnTestStoppedAsync);

    public async Task StartRuntimeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (runningCts != null)
        {
            return;
        }

        runtimeStopping = false;
        runningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken runtimeToken = runningCts.Token;
        await RefreshStationEnabledStatesAsync(runtimeToken).ConfigureAwait(false);
        var dispatchQueue = new StationResultDispatchQueue();
        stationResultDispatchQueue = dispatchQueue;
        stationResultDispatchTask = Task.Run(() => dispatchQueue.RunAsync(this, runtimeToken), CancellationToken.None);
        ioSnapshotTask = StartIoSnapshotThread(runtimeToken);
        machinePollingTask = Task.Run(() => RunMachinePollingAsync(runtimeToken), CancellationToken.None);

        foreach (TestStationModel station in TestStations)
        {
            // Display-only stations can still be shown and switched by PLC, but they must not poll default IO 0.
            if (station.StationDataDeals.Count == 0)
            {
                continue;
            }
            var orchestrator = new CompositeDataDeal(this, station.StationDataDeals, dispatchQueue)
            {
                TriggerMode = StationTriggerMode
            };
            orchestrators[station.StationId] = orchestrator;
            stationLifecycleTasks.Add(StartStationLifecycleThread(orchestrator, station, runtimeToken));
        }
    }

    private static Task StartStationLifecycleThread(
        CompositeDataDeal orchestrator,
        TestStationModel station,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                orchestrator.RunLifecycleBlocking(station, cancellationToken);
                completion.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = $"StationRealtime-{station.StationId}"
        };

        thread.Start();
        return completion.Task;
    }

    private Task StartIoSnapshotThread(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                RunIoSnapshotLoop(cancellationToken);
                completion.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "MachineIoSnapshot"
        };

        thread.Start();
        return completion.Task;
    }

    public async Task RefreshStationEnabledStatesAsync(CancellationToken cancellationToken = default)
    {
        foreach (TestStationModel station in TestStations)
        {
            try
            {
                bool? isEnabled = await ReadStationEnabledAsync(station, cancellationToken).ConfigureAwait(false);
                if (isEnabled.HasValue)
                {
                    SetStationEnabledState(station, isEnabled.Value);
                }
            }
            catch
            {
                // Station switch read failure must not block runtime startup.
            }
        }
    }

    public async Task StopRuntimeAsync()
    {
        if (runningCts == null)
        {
            return;
        }

        runtimeStopping = true;
        CancellationTokenSource cts = runningCts;
        runningCts = null;
        cts.Cancel();

        if (ioSnapshotTask != null)
        {
            try
            {
                await ioSnapshotTask.WaitAsync(StopTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch
            {
            }

            ioSnapshotTask = null;
        }
        if (machinePollingTask != null)
        {
            try
            {
                await machinePollingTask.WaitAsync(StopTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }

            machinePollingTask = null;
        }

        if (stationLifecycleTasks.Count > 0)
        {
            Task[] tasks = stationLifecycleTasks.ToArray();
            stationLifecycleTasks.Clear();
            try
            {
                await Task.WhenAll(tasks).WaitAsync(StopTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch
            {
            }
        }
        stationResultDispatchQueue?.Complete();
        if (stationResultDispatchTask != null)
        {
            try
            {
                await stationResultDispatchTask.WaitAsync(StopTimeout).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch
            {
            }

            stationResultDispatchTask = null;
        }

        stationResultDispatchQueue = null;
        cts.Dispose();
        orchestrators.Clear();
        ResetIoSnapshot();
    }

    private async Task StopProductionCoreAsync(Func<CancellationToken, Task> stateChanged)
    {
        if (!isRunning)
        {
            return;
        }

        isRunning = false;
        productionState = MachineProductionState.Stopped;
        try
        {
            await stateChanged(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            RaiseRunningStateChanged();
        }
    }

    protected async Task NotifyExternalStartAsync(CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task>? handlers = ExternalStartRequested;
        if (handlers == null)
        {
            return;
        }

        foreach (Func<CancellationToken, Task> handler in handlers.GetInvocationList().Cast<Func<CancellationToken, Task>>())
        {
            try
            {
                await handler(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 外部启动提示属于附加体验，不应影响既有参数重写和启动握手。
            }
        }
    }
    private void RaiseRunningStateChanged()
        => RunningStateChanged?.Invoke(this, EventArgs.Empty);

    public async Task ExecuteStationAsync(int stationId, bool triggerResult = true, CancellationToken cancellationToken = default)
    {
        TestStationModel station = TestStations.First(item => item.StationId == stationId);
        var orchestrator = new CompositeDataDeal(this, station.StationDataDeals)
        {
            TriggerMode = TriggerMode.Programmatic
        };
        await orchestrator.ExecuteMeasurementAsync(triggerResult, station, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<StationOperationDescriptor> GetStationOperations(TestStationModel station)
    {
        ArgumentNullException.ThrowIfNull(station);
        return station.Operations;
    }

    /// <summary>
    /// 设置工位启用状态。默认只修改内存状态，具体机型可覆盖并写入 PLC 工位开关点位。
    /// </summary>
    public virtual Task SetStationEnabledAsync(TestStationModel station, bool isEnabled, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        SetStationEnabledState(station, isEnabled);
        return Task.CompletedTask;
    }

    public virtual Task<bool?> ReadStationEnabledAsync(TestStationModel station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        return Task.FromResult<bool?>(station.IsEnabled);
    }

    public void SetStationEnabledState(TestStationModel station, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (station.IsEnabled == isEnabled)
        {
            return;
        }

        station.IsEnabled = isEnabled;
        StationEnabledChanged?.Invoke(this, new StationEnabledChangedEventArgs(station, isEnabled));
    }

    public virtual Task<bool> ExecuteStationOperationAsync(
        TestStationModel station,
        string operationCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationCode);
        return Task.FromResult(false);
    }

    /// <summary>
    /// 将测试上下限写入工位模型，供 PC 判定、DataGrid 和图表共用。
    /// </summary>
    /// <summary>
    /// 当 MES 或本地配置更新参数时，子类可调用该方法同步软件判定上下限。
    /// </summary>
    protected void SetStationTestLimit(string testName, double? lowerLimit, double? upperLimit, string? unit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        foreach (TestStationModel station in TestStations)
        {
            if (station.OrderedTestNames.Any(item => string.Equals(item, testName, StringComparison.OrdinalIgnoreCase)))
            {
                station.SetTestLimit(testName, lowerLimit, upperLimit, unit);
            }
        }
    }
    public bool TryResolveStationTest(string parameterId, out TestStationModel? station, out string testName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);

        foreach (TestStationModel candidate in TestStations)
        {
            foreach (string candidateTestName in candidate.OrderedTestNames)
            {
                if (string.Equals(parameterId, candidateTestName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parameterId, CreateCellKey(candidate.StationId, candidateTestName), StringComparison.OrdinalIgnoreCase))
                {
                    station = candidate;
                    testName = candidateTestName;
                    return true;
                }
            }
        }

        station = null;
        testName = string.Empty;
        return false;
    }

    public void RefreshStationLimitsFromInstrumentConfigs()
    {
        foreach (TestStationModel station in TestStations)
        {
            foreach (string deviceId in station.InstrumentDeviceIds.Where(static id => !string.IsNullOrWhiteSpace(id)))
            {
                if (Devices.TryGet(deviceId, out IMeasurementLimitSetProvider? limitSetProvider)
                    && limitSetProvider != null
                    && limitSetProvider.TryGetMeasurementLimits(out IReadOnlyDictionary<string, InstrumentMeasurementLimit>? limits))
                {
                    foreach (string testName in station.OrderedTestNames)
                    {
                        if (limits.TryGetValue(testName, out InstrumentMeasurementLimit? limit))
                        {
                            station.SetTestLimit(testName, limit.LowerLimit, limit.UpperLimit, limit.Unit);
                        }
                    }

                    continue;
                }

                if (!Devices.TryGet(deviceId, out IMeasurementLimitProvider? limitProvider)
                    || limitProvider == null
                    || !limitProvider.TryGetMeasurementLimit(out InstrumentMeasurementLimit? sharedLimit))
                {
                    continue;
                }

                foreach (string testName in station.OrderedTestNames)
                {
                    station.SetTestLimit(testName, sharedLimit.LowerLimit, sharedLimit.UpperLimit, sharedLimit.Unit);
                }
            }
        }

        UpdateLimitRows();
        RaiseTableChanged();
    }

    /// <summary>
    /// Reapplies the current configuration of every instrument bound to a test
    /// station. Used by parameter-compare handshakes and offline PC startup.
    /// </summary>
    public async Task ApplyStationInstrumentConfigsAsync(CancellationToken cancellationToken = default)
    {
        foreach (string deviceId in TestStations
            .SelectMany(static station => station.InstrumentDeviceIds)
            .Where(static deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Devices.TryGet<IConfigurableDevice>(deviceId, out IConfigurableDevice? device) && device != null)
            {
                await device.ApplyConfigAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected void UpdateLimitRows()
    {
        DisplayRowItem row = GetRow("Limits");
        foreach (TestStationModel station in TestStations)
        {
            foreach (string testName in station.OrderedTestNames)
            {
                string key = CreateCellKey(station.StationId, testName);
                if (station.TestLimits.TryGetValue(testName, out StationMeasurementLimit? limit))
                {
                    SetCellValue(row, key, FormatLimitText(limit));
                }
            }
        }
    }

    private static string? FormatLimitText(StationMeasurementLimit limit)
    {
        if (!limit.LowerLimit.HasValue && !limit.UpperLimit.HasValue)
        {
            return null;
        }

        string lower = FormatLimitNumber(limit.LowerLimit);
        string upper = FormatLimitNumber(limit.UpperLimit);
        return string.IsNullOrWhiteSpace(limit.Unit)
            ? $"{lower}~{upper}"
            : $"{lower}~{upper} {limit.Unit}";
    }
    private static string FormatLimitNumber(double? value)
        => value?.ToString("0.##########", CultureInfo.InvariantCulture) ?? string.Empty;

public virtual Task ApplyWorkOrderSetupAsync(MesWorkOrderSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return Task.CompletedTask;
    }
    /// <summary>
    /// 设置点检界面进入状态。默认机型不需要写 PLC。
    /// </summary>
    public virtual Task SetCheckViewActiveAsync(bool isActive, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// 点检页面进入后的机型专属处理。默认机型无需额外处理。
    /// </summary>
    public virtual Task OnCheckViewEnteredAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// 设置 PLC 点检完成信号。默认机型不需要该信号。
    /// </summary>
    public virtual Task SetCheckCompletedAsync(bool isCompleted, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// 读取 PLC 点检完成信号。返回 null 表示当前机型未配置该点位或 PLC 不可用，调用方不应将其误判为未完成。
    /// </summary>
    public virtual Task<bool?> ReadCheckCompletedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<bool?>(null);

    /// <summary>
    /// 设置 PLC 标准件过期信号。默认机型不需要该信号。
    /// </summary>
    public virtual Task SetStandardSampleExpiredAsync(bool isExpired, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// 自动点检时间窗口超时且未点检成功时触发。默认机型只弹窗提醒，不执行硬件动作。
    /// </summary>
    public virtual Task<MachineExamineResult> ExecuteExamineAsync(string flowCode, IProgress<MachineExamineMeasurement>? progress = null, CancellationToken cancellationToken = default)
        => Task.FromResult(MachineExamineResult.Failed($"Machine does not support examine flow: {flowCode}."));

    public virtual Task<MachineExamineResult> ExecuteExamineStandardAsync(IProgress<MachineExamineMeasurement>? progress = null, CancellationToken cancellationToken = default)
        => ExecuteExamineAsync("Standard", progress, cancellationToken);

    /// <summary>
    /// 执行确认件点检。默认机型不支持，由具体机型实现。
    /// </summary>
    public virtual Task<MachineExamineResult> ExecuteExamineConfirmAsync(IProgress<MachineExamineMeasurement>? progress = null, CancellationToken cancellationToken = default)
        => ExecuteExamineAsync("Confirm", progress, cancellationToken);

    protected async Task<MachineExamineResult> ExecuteExamineFlowAsync(MachineExamineFlowDescriptor flow, IProgress<MachineExamineMeasurement>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);

        IPlcDevice? plc = Plc;
        var measurements = new List<MachineExamineMeasurement>();
        if (plc == null || !plc.IsConnected)
        {
            return MachineExamineResult.Failed("PLC is not connected.", measurements);
        }

        await plc.WriteInt16Async(PlcAddressCache[flow.SamplePointKey], 1, cancellationToken).ConfigureAwait(false);
        await plc.WriteInt16Async(PlcAddressCache[flow.StartPointKey], 1, cancellationToken).ConfigureAwait(false);

        int repeatCount = Math.Max(1, flow.RepeatCount);
        for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
        {
            foreach (MachineExamineStepDescriptor step in flow.Steps)
            {
                bool ready = await WaitPlcSignalAsync(plc, PlcAddressCache[step.TriggerPointKey], (ushort)1, timeoutMs: step.TimeoutMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!ready)
                {
                    return MachineExamineResult.Failed(measurements: measurements);
                }

                MachineExamineMeasurement? measurement = await ReadStationMeasurementAsync(step.StationId, step.TestName, cancellationToken).ConfigureAwait(false);
                if (measurement == null)
                {
                    return MachineExamineResult.Failed(measurements: measurements);
                }

                measurements.Add(measurement);
                progress?.Report(measurement);
                await plc.WriteInt16Async(PlcAddressCache[step.ReadCompletedPointKey], 1, cancellationToken).ConfigureAwait(false);
            }
        }

        await plc.WriteInt16Async(PlcAddressCache[flow.CompletedPointKey], 1, cancellationToken).ConfigureAwait(false);
        return MachineExamineResult.Completed(measurements);
    }

    protected async Task<MachineExamineMeasurement?> ReadStationMeasurementAsync(int stationId, string testName, CancellationToken cancellationToken)
    {
        IStationInstrumentOperation? operation = GetStationInstrument(stationId, testName);
        if (operation == null)
        {
            return null;
        }

        InstrumentMeasurementResult value = await operation.MeasureBySoftwareTriggerAsync(cancellationToken).ConfigureAwait(false);
        return new MachineExamineMeasurement(stationId, TestStations.FirstOrDefault(station => station.StationId == stationId)?.StationName ?? stationId.ToString(CultureInfo.InvariantCulture), testName, value);
    }

    protected IStationInstrumentOperation? GetStationInstrument(int stationId, string testName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        TestStationModel? station = TestStations.FirstOrDefault(item => item.StationId == stationId);
        return GetStationInstrument(station, testName);
    }

    protected IStationInstrumentOperation? GetStationInstrument(TestStationModel? station, string testName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        return station?.StationDataDeals
            .OfType<IStationInstrumentOperation>()
            .FirstOrDefault(operation => IsInstrumentOperationMatch(operation.TestName, testName));
    }

    private static bool IsInstrumentOperationMatch(string operationTestName, string requestedTestName)
    {
        if (string.Equals(operationTestName, requestedTestName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return operationTestName
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, requestedTestName, StringComparison.OrdinalIgnoreCase));
    }

    public MachinePlcPointDefinition GetPlcPoint(string key)
    {
        if (plcPointMap.TryGetValue(key, out MachinePlcPointDefinition? point))
        {
            return point;
        }

        throw new KeyNotFoundException($"PLC point not found: {key}");
    }

    public string GetPlcAddress(int pointKey)
    {
        if (PlcAddressCache.TryGetValue(pointKey, out string? address))
        {
            return address;
        }

        throw new KeyNotFoundException($"PLC address not found. PointKey={pointKey}.");
    }

    public Task<bool> WaitPlcSignalAsync(
        IPlcDevice? plc,
        string address,
        bool expectedState = true,
        int timeoutMs = 5000,
        int intervalMs = 1,
        CancellationToken cancellationToken = default)
        => WaitPlcSignalAsync<bool>(plc, address, expectedState, timeoutMs, intervalMs, cancellationToken);

    public async Task<bool> WaitPlcSignalAsync<TValue>(
        IPlcDevice? plc,
        string address,
        TValue expectedValue,
        int timeoutMs = 5000,
        int intervalMs = 1,
        CancellationToken cancellationToken = default)
    {
        if (plc == null || !plc.IsConnected || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            while (!timeoutCts.IsCancellationRequested)
            {
                TValue actualValue = await ReadPlcSignalValueAsync<TValue>(plc, address, timeoutCts.Token).ConfigureAwait(false);
                if (EqualityComparer<TValue>.Default.Equals(actualValue, expectedValue))
                {
                    return true;
                }

                await Task.Delay(intervalMs, timeoutCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return false;
    }

    private static async Task<TValue> ReadPlcSignalValueAsync<TValue>(
        IPlcDevice plc,
        string address,
        CancellationToken cancellationToken)
    {
        Type valueType = typeof(TValue);

        if (valueType == typeof(bool))
        {
            bool value = await plc.ReadBoolAsync(address, cancellationToken).ConfigureAwait(false);
            return (TValue)(object)value;
        }

        if (valueType == typeof(short))
        {
            short value = await plc.ReadInt16Async(address, cancellationToken).ConfigureAwait(false);
            return (TValue)(object)value;
        }

        if (valueType == typeof(ushort))
        {
            short value = await plc.ReadInt16Async(address, cancellationToken).ConfigureAwait(false);
            return (TValue)(object)unchecked((ushort)value);
        }

        if (valueType == typeof(int))
        {
            int[] values = await plc.ReadInt32ArrayAsync(address, 1, cancellationToken).ConfigureAwait(false);
            return (TValue)(object)values[0];
        }

        throw new NotSupportedException($"Unsupported PLC signal value type: {valueType.FullName}");
    }
    public bool IsDiOn(int channel)
    {
        ValidateIoChannel(channel);
        ulong bit = 1UL << channel;
        lock (ioSnapshotSync)
        {
            return (currentDiMask & bit) != 0;
        }
    }

    public bool IsRisingEdge(int channel)
    {
        ValidateIoChannel(channel);
        ulong bit = 1UL << channel;
        lock (ioSnapshotSync)
        {
            if ((latchedRisingDiMask & bit) == 0)
            {
                return false;
            }

            latchedRisingDiMask &= ~bit;
            return true;
        }
    }

    public bool IsFallingEdge(int channel)
    {
        ValidateIoChannel(channel);
        ulong bit = 1UL << channel;
        lock (ioSnapshotSync)
        {
            if ((latchedFallingDiMask & bit) == 0)
            {
                return false;
            }

            latchedFallingDiMask &= ~bit;
            return true;
        }
    }

    protected void RegisterAndCachePlcPoints<TEnum>()
        where TEnum : struct, Enum
    {
        foreach (PlcPointMetadataItem item in PropertyMetadataReader.GetPlcPoints<TEnum>())
        {
            int numericKey = Convert.ToInt32(item.Value);
            var definition = new MachinePlcPointDefinition(item.Name, item.Address, item.DisplayName, item.DataType, item.IsReadOnly);

            plcPointMap[item.Name] = definition;
            PlcAddressCache[numericKey] = item.Address;

            int existingIndex = -1;
            for (int i = 0; i < PlcPointDefinitions.Count; i++)
            {
                if (string.Equals(PlcPointDefinitions[i].Key, item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                PlcPointDefinitions[existingIndex] = definition;
            }
            else
            {
                PlcPointDefinitions.Add(definition);
            }

            Plc?.RegisterPoint(item.Address, item.DisplayName, item.DataType, item.IsReadOnly);
        }
    }

    protected void RegisterPlcPoint<TPoint>(TPoint point, string address, string displayName, Type dataType, bool isReadOnly = false)
        where TPoint : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(dataType);

        int numericKey = Convert.ToInt32(point);
        string key = point.ToString();
        var definition = new MachinePlcPointDefinition(key, address, displayName, dataType, isReadOnly);

        plcPointMap[key] = definition;
        PlcAddressCache[numericKey] = address;

        int existingIndex = -1;
        for (int i = 0; i < PlcPointDefinitions.Count; i++)
        {
            if (string.Equals(PlcPointDefinitions[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            PlcPointDefinitions[existingIndex] = definition;
        }
        else
        {
            PlcPointDefinitions.Add(definition);
        }

        Plc?.RegisterPoint(address, displayName, dataType, isReadOnly);
    }

    protected IEnumerable<MachinePlcPointDefinition> GetPlcPoints<TPoint>(params TPoint[] points)
        where TPoint : struct, Enum
    {
        foreach (TPoint point in points)
        {
            yield return GetPlcPoint(point.ToString());
        }
    }

    protected void BuildDataGrid()
    {
        RefreshResultGridTestNames();

        PartColumns.Clear();
        PartRows.Clear();
        partRowMap.Clear();

        PartColumns.Add(new DataGridColumnDescriptor { ParameterId = "RowName", DisplayName = T("Flow.Grid.Project", "项目") });
        foreach (TestStationModel station in TestStations)
        {
            foreach (string testName in station.OrderedTestNames)
            {
                string key = CreateCellKey(station.StationId, testName);
                PartColumns.Add(new DataGridColumnDescriptor { ParameterId = key, DisplayName = testName });
            }
        }

        AddRow("Limits", T("Flow.Grid.Limits", "上下限"), null);
        AddRow("TestValue", T("Flow.Grid.TestValue", "测试值"), null);
        AddRow("Total", T("Flow.Grid.Total", "总数"), null);
        AddRow("Ok", T("Flow.Grid.Ok", "OK数"), null);
        AddRow("Ng", T("Flow.Grid.Ng", "NG数"), null);
        AddRow("Yield", T("Flow.Grid.Yield", "良率"), null);
        RaiseTableChanged();
    }

    private void RefreshResultGridTestNames()
    {
        foreach (TestStationModel station in TestStations)
        {
            if (!station.ShowInResultGrid)
            {
                station.OrderedTestNames = [];
                continue;
            }

            if (!station.UseInstrumentConfigTestNames)
            {
                continue;
            }

            IMeasurementInstrument? meter = ResolveStationInstrument(station);
            IReadOnlyList<string> testNames = InstrumentMeasurementNameHelper.CreateTestNames(meter);
            station.OrderedTestNames = testNames.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    private IMeasurementInstrument? ResolveStationInstrument(TestStationModel station)
    {
        foreach (string deviceId in station.InstrumentDeviceIds.Where(static id => !string.IsNullOrWhiteSpace(id)))
        {
            if (Devices.TryGet<IMeasurementInstrument>(deviceId, out IMeasurementInstrument? meter) && meter != null)
            {
                return meter;
            }
        }

        return null;
    }
    public void UpdateTestValues(TestStationModel station)
    {
        DisplayRowItem row = GetRow("TestValue");
        foreach (string testName in station.OrderedTestNames)
        {
            string key = CreateCellKey(station.StationId, testName);
            SetCellValue(row, key, station.TestValues.TryGetValue(testName, out double value) ? value.ToString("F4") : null);
        }

        RaiseTableChanged();
    }

    public void UpdateTestResults(TestStationModel station)
    {
        DisplayRowItem row = GetRow("TestValue");
        foreach (string testName in station.OrderedTestNames)
        {
            string key = CreateCellKey(station.StationId, testName);
            row.UpdateJudge(key, station.TestJudges.TryGetValue(testName, out bool ok) ? ok : null);
        }

        RaiseTableChanged();
    }

    public void UpdateStatistics(TestStationModel station, bool isPass)
    {
        station.AccumulateResult(isPass);

        foreach (string testName in station.OrderedTestNames)
        {
            string key = CreateCellKey(station.StationId, testName);
            SetCellValue(GetRow("Total"), key, station.TotalCount.ToString());
            SetCellValue(GetRow("Ok"), key, station.OkCount.ToString());
            SetCellValue(GetRow("Ng"), key, station.NgCount.ToString());
            SetCellValue(GetRow("Yield"), key, station.YieldRate.ToString("P2"));
        }

        RaiseTableChanged();
    }

    protected void UpdateStatisticsRows(TestStationModel station, string testName, uint totalCount, uint okCount, uint ngCount, double yieldRate)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);

        string key = CreateCellKey(station.StationId, testName);

        SetCellValue(GetRow("Total"), key, totalCount.ToString(CultureInfo.InvariantCulture));
        SetCellValue(GetRow("Ok"), key, okCount.ToString(CultureInfo.InvariantCulture));
        SetCellValue(GetRow("Ng"), key, ngCount.ToString(CultureInfo.InvariantCulture));
        SetCellValue(GetRow("Yield"), key, yieldRate.ToString("P2", CultureInfo.InvariantCulture));
        RaiseTableChanged();
    }
    public void ClearDataGrid()
    {
        foreach (TestStationModel station in TestStations)
        {
            station.TestValues.Clear();
            station.TestJudges.Clear();
            station.ResetStatistics();
        }

        foreach (DisplayRowItem row in PartRows)
        {
            foreach (CellState cell in row.Cells.Values)
            {
                cell.Value = null;
                cell.Judge = null;
            }
        }

        RaiseTableChanged();
    }

    protected virtual void ReadSystemData()
    {
    }

    protected virtual Task ProcessTestRecordAsync(TestResultPayload record, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// 读取工位测试完成信号的上升沿。
    /// 默认从 IO 快照读取 StationIo.TestFinishedInput，子类可覆盖特殊触发来源。
    /// </summary>
    public virtual bool ReadStationTrigger(TestStationModel station)
    {
        ArgumentNullException.ThrowIfNull(station);
        return station.StationIo.TestFinishedInput >= 0 && IsRisingEdge(station.StationIo.TestFinishedInput);
    }

    /// <summary>
    /// 读取工位判定结果。Hardware 模式从 ResultOkInput/ResultNgInput 读 IO，Software 模式由 DataDeal 判定。
    /// </summary>
    public virtual bool ReadStationResult(TestStationModel station)
    {
        ArgumentNullException.ThrowIfNull(station);

        StationIoBinding io = station.StationIo;
        if (io.ResultSource == StationResultSource.Software)
        {
            return true;
        }

        if (io.ResultNgInput >= 0 && IsDiOn(io.ResultNgInput))
        {
            return false;
        }

        if (io.ResultOkInput >= 0)
        {
            return IsDiOn(io.ResultOkInput);
        }

        return true;
    }

    public bool TryReadDiSnapshotBit(int channel, out bool state)
    {
        state = false;
        if (channel < 0 || channel >= MaxIoChannelCount)
        {
            return false;
        }

        lock (ioSnapshotSync)
        {
            ulong bit = 1UL << channel;
            state = (currentDiMask & bit) != 0;
            return true;
        }
    }
    /// <summary>
    /// 向 StationIo.ResultReadCompletedOutput 输出 5ms 读取完成脉冲。
    /// </summary>
    public virtual Task CompleteStationHandshakeAsync(TestStationModel station, bool isPass, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (runtimeStopping || IoCard == null || !IoCard.IsConnected)
        {
            return Task.CompletedTask;
        }

        StationIoBinding io = station.StationIo;
        WriteResultOutputs(io, isPass);

        if (io.ResultReadCompletedOutput < 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (!runtimeStopping && IoCard is { IsConnected: true })
            {
                IoCard.WriteDoBit(io.ResultReadCompletedOutput, true);
            }

            DelayRuntimeLoop(5, cancellationToken);
        }
        finally
        {
            if (!runtimeStopping && IoCard is { IsConnected: true })
            {
                try
                {
                    IoCard.WriteDoBit(io.ResultReadCompletedOutput, false);
                }
                catch
                {
                }
            }
        }

        return Task.CompletedTask;
    }

    private void WriteResultOutputs(StationIoBinding io, bool isPass)
    {
        if (runtimeStopping || IoCard == null || !IoCard.IsConnected)
        {
            return;
        }

        if (io.ResultOkOutput >= 0)
        {
            IoCard.WriteDoBit(io.ResultOkOutput, isPass);
        }

        if (io.ResultNgOutput >= 0)
        {
            IoCard.WriteDoBit(io.ResultNgOutput, !isPass);
        }
    }

    internal Task ProcessTestRecordCoreAsync(TestResultPayload record, CancellationToken cancellationToken)
        => ProcessTestRecordAsync(record, cancellationToken);

    internal async Task ProcessStationResultAsync(StationResultMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.ApplyToStation();
        ApplyStationResultToTable(message);
        RaiseStationResultPublished(message);

        foreach (StationResultValue value in message.Values)
        {
            await ProcessTestRecordCoreAsync(
                new TestResultPayload(RecordType.Numeric, value.TestName, value.Value, value.Judge),
                cancellationToken).ConfigureAwait(false);
        }
    }

    protected virtual bool ShouldApplyRealtimeStatisticsToTable => true;

    private void ApplyStationResultToTable(StationResultMessage message)
    {
        TestStationModel station = message.Station;
        bool applyRealtimeStatistics = ShouldApplyRealtimeStatisticsToTable;
        if (applyRealtimeStatistics)
        {
            station.AccumulateResult(message.IsPass);
        }

        DisplayRowItem testValueRow = GetRow("TestValue");
        DisplayRowItem? totalRow = applyRealtimeStatistics ? GetRow("Total") : null;
        DisplayRowItem? okRow = applyRealtimeStatistics ? GetRow("Ok") : null;
        DisplayRowItem? ngRow = applyRealtimeStatistics ? GetRow("Ng") : null;
        DisplayRowItem? yieldRow = applyRealtimeStatistics ? GetRow("Yield") : null;

        foreach (StationResultValue value in message.Values)
        {
            string key = CreateCellKey(station.StationId, value.TestName);
            SetCellValue(testValueRow, key, value.Value.ToString("F4", CultureInfo.InvariantCulture));
            testValueRow.UpdateJudge(key, value.Judge);

            if (applyRealtimeStatistics)
            {
                SetCellValue(totalRow!, key, station.TotalCount.ToString(CultureInfo.InvariantCulture));
                SetCellValue(okRow!, key, station.OkCount.ToString(CultureInfo.InvariantCulture));
                SetCellValue(ngRow!, key, station.NgCount.ToString(CultureInfo.InvariantCulture));
                SetCellValue(yieldRow!, key, station.YieldRate.ToString("P2", CultureInfo.InvariantCulture));
            }
        }

        RaiseTableChanged();
    }
    protected virtual Task OnTestStartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected virtual Task OnTestStoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected static string CreateCellKey(int stationId, string testName)
        => $"S{stationId}_{testName}";

    protected void RaiseTableChanged()
        => TableChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseStationResultPublished(StationResultMessage message)
    {
        var values = message.Values
            .Select(static value => new TestResultPayload(RecordType.Numeric, value.TestName, value.Value, value.Judge))
            .ToArray();
        StationResultPublished?.Invoke(this, new StationResultPublishedEventArgs(message.Station, values, message.IsPass));
    }

    private async Task RunMachinePollingAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadSystemData();
            await DelayRuntimeLoopAsync(SystemDataPollingIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RunIoSnapshotLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            RefreshIoSnapshot();
            DelayRuntimeLoop(IoSnapshotPollingIntervalMs, cancellationToken);
        }
    }

    public void RefreshIoSnapshot()
    {
        if (runtimeStopping)
        {
            return;
        }

        IIoCardDevice? card = IoCard;
        if (card == null || !card.IsConnected)
        {
            return;
        }

        ulong mask = card.ReadDiPortMask();
        lock (ioSnapshotSync)
        {
            ulong oldMask = currentDiMask;
            previousDiMask = oldMask;
            currentDiMask = mask;
            latchedRisingDiMask |= mask & ~oldMask;
            latchedFallingDiMask |= ~mask & oldMask;
        }
    }

    private static async Task DelayRuntimeLoopAsync(int delayMs, CancellationToken cancellationToken)
    {
        if (delayMs <= 0)
        {
            await Task.Yield();
            return;
        }

        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
    }

    private static void DelayRuntimeLoop(int delayMs, CancellationToken cancellationToken)
    {
        if (delayMs <= 0)
        {
            Thread.Yield();
            return;
        }

        if (cancellationToken.WaitHandle.WaitOne(delayMs))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void ResetIoSnapshot()
    {
        lock (ioSnapshotSync)
        {
            previousDiMask = 0;
            currentDiMask = 0;
            latchedRisingDiMask = 0;
            latchedFallingDiMask = 0;
        }
    }

    private void AddRow(string key, string name, object? defaultValue)
    {
        var row = new DisplayRowItem { RowName = name };
        partRowMap[key] = row;
        foreach (DataGridColumnDescriptor column in PartColumns.Skip(1))
        {
            SetCellValue(row, column.ParameterId, defaultValue);
        }

        PartRows.Add(row);
    }

    private DisplayRowItem GetRow(string key)
    {
        if (partRowMap.TryGetValue(key, out DisplayRowItem? row))
        {
            return row;
        }

        return PartRows.First(item => string.Equals(item.RowName, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.RowName, ToRowDisplayName(key), StringComparison.OrdinalIgnoreCase));
    }

    private static void SetCellValue(DisplayRowItem row, string parameterId, object? value)
    {
        row.UpdateCell(parameterId, value);
    }

    private string ToRowDisplayName(string key)
        => key switch
        {
            "Limits" => T("Flow.Grid.Limits", "上下限"),
            "TestValue" => T("Flow.Grid.TestValue", "测试值"),
            "Total" => T("Flow.Grid.Total", "总数"),
            "Ok" => T("Flow.Grid.Ok", "OK数"),
            "Ng" => T("Flow.Grid.Ng", "NG数"),
            "Yield" => T("Flow.Grid.Yield", "良率"),
            _ => key
        };

    private void OnLanguageChanged(object? sender, LanguageType languageType)
        => RefreshResultGridLocalization();

    private void RefreshResultGridLocalization()
    {
        DataGridColumnDescriptor? rowNameColumn = PartColumns.FirstOrDefault(static column =>
            string.Equals(column.ParameterId, "RowName", StringComparison.OrdinalIgnoreCase));
        if (rowNameColumn != null)
        {
            rowNameColumn.DisplayName = T("Flow.Grid.Project", "项目");
            int index = PartColumns.IndexOf(rowNameColumn);
            if (index >= 0)
            {
                PartColumns.RemoveAt(index);
                PartColumns.Insert(index, rowNameColumn);
            }
        }

        foreach ((string key, DisplayRowItem row) in partRowMap)
        {
            row.RowName = ToRowDisplayName(key);
        }

        RaiseTableChanged();
    }

    public void RefreshResultGrid()
    {
        BuildDataGrid();
        UpdateLimitRows();
        RaiseTableChanged();
    }

    private string T(string key, string fallback)
    {
        string? text = localizationService?.GetString(key);
        return string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal) ? fallback : text;
    }

    private static void ValidateIoChannel(int channel)
    {
        if (channel is < 0 or >= MaxIoChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, $"IO channel must be between 0 and {MaxIoChannelCount - 1}.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (localizationService != null)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
        }

        StopRuntimeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}

public sealed class StationEnabledChangedEventArgs : EventArgs
{
    public StationEnabledChangedEventArgs(TestStationModel station, bool isEnabled)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
        IsEnabled = isEnabled;
    }

    public TestStationModel Station { get; }

    public bool IsEnabled { get; }
}

public sealed class StationResultPublishedEventArgs : EventArgs
{
    public StationResultPublishedEventArgs(TestStationModel station, IReadOnlyList<TestResultPayload> values, bool isPass)
    {
        Station = station ?? throw new ArgumentNullException(nameof(station));
        Values = values ?? throw new ArgumentNullException(nameof(values));
        IsPass = isPass;
    }

    public TestStationModel Station { get; }

    public IReadOnlyList<TestResultPayload> Values { get; }

    public bool IsPass { get; }
}
public enum RecordType
{
    Numeric,
    Text,
    Boolean,
    Unknown
}

public sealed record TestResultPayload(RecordType Type, string Name, double TestValue, bool? Judge = null);

















