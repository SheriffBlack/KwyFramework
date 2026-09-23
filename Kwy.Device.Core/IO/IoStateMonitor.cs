using Kwy.Device.Abstractions.IO;
using System.Collections.Concurrent;

namespace Kwy.Device.Core.IO;

/// <summary>
/// 逻辑 IO 运行时服务。
/// 负责校验 <see cref="IoPointDefinition"/> 映射、维护 DI 采集快照，并将业务 <c>pointId</c> 转换为物理设备和通道。
/// 它是上位机软件的状态与工艺输出层，不承担确定实时控制或功能安全职责。
/// </summary>
public sealed class IoStateMonitor : IIoStateMonitor, ILogicalIoReader, ILogicalIoWriter, IProcessOutputStateController, ILogicalIoInterruptWaiter
{
    // 当前配置中可用的物理 IO 设备，可以是独立 IO 卡或运动卡板载 IO。
    private readonly ConcurrentDictionary<string, IIoCardDevice> _devices = new();

    // 稳定逻辑点位 ID 到物理点位定义的映射。
    private readonly Dictionary<string, IoPointDefinition> _diMap = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IoPointDefinition> _doMap = new(StringComparer.OrdinalIgnoreCase);

    // 每张设备最近一次的物理 DI 快照，用于读取缓存状态和计算边沿变化。
    private readonly ConcurrentDictionary<string, ulong> _deviceMaskCache = new();
    private readonly object _maskSync = new();
    public event Action<string, bool>? OnIoStateChanged;

    private readonly Dictionary<string, string[]> _fastReverseDiMap = new();

    private CancellationTokenSource? _scanCancellation;
    private Task? _scanTask;
    private readonly ConcurrentDictionary<string, EventHandler<IoSignalSnapshot>> _interruptHandlers = new();
    private readonly ConcurrentDictionary<string, PulseOutputScheduler> _logicalPulseSchedulers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IoStateMonitorOptions _options;
    private bool _disposed;

    public event Action<string, Exception>? OnIoReadFailed;

    public event Action<string, Exception>? OnIoWriteFailed;

    public event Action<string, Exception>? OnIoNotificationFailed;

    public event Action<IoSignalSnapshot>? OnIoSnapshotReceived;

    public IoStateMonitor(IoStateMonitorOptions? options = null)
    {
        _options = options ?? new IoStateMonitorOptions();
        _options.Validate();
    }

    /// <summary>
    /// 校验物理设备与点位定义后启动状态采集。采集用于软件状态、HMI 和动作前检查，不是实时或功能安全机制。
    /// </summary>
    public void Initialize(IEnumerable<IIoCardDevice> devices, IIoPointDefinitionProvider pointDefinitions)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(pointDefinitions);

        IIoCardDevice[] deviceItems = devices.ToArray();
        IoPointDefinition[] diItems = pointDefinitions.GetByKind(IoSignalKind.DigitalInput).ToArray();
        IoPointDefinition[] doItems = pointDefinitions.GetByKind(IoSignalKind.DigitalOutput).ToArray();
        InitializationMaps maps = BuildAndValidateMaps(deviceItems, diItems, doItems);

        // 点位定义目录不归监视器所有；候选绑定全部校验通过后再替换运行状态，避免无效配置破坏已有采集。
        Reset();

        foreach (IIoCardDevice dev in deviceItems)
        {
            _devices[dev.DeviceId] = dev;
            // 为每张卡预分配一个数组（支持最大 64 通道）
            _fastReverseDiMap[dev.DeviceId] = new string[64];

            // 可选中断用于更快地更新上位机状态；回调、线程调度和订阅者均不具备确定实时性。
            if (dev is IHardwareInterruptSource interruptSource)
            {
                EventHandler<IoSignalSnapshot> handler = (_, snapshot) => ProcessSignalSnapshot(snapshot);
                _interruptHandlers[dev.DeviceId] = handler;
                interruptSource.HardwareInterruptReceived += handler;
            }
        }

        foreach ((string id, IoPointDefinition point) in maps.DiMap)
        {
            _diMap[id] = point;
            _fastReverseDiMap[point.DeviceId][point.Channel] = id;
        }
        foreach ((string id, IoPointDefinition point) in maps.DoMap)
        {
            _doMap[id] = point;
        }

        StartHeartbeat();
    }

    private static InitializationMaps BuildAndValidateMaps(
        IReadOnlyCollection<IIoCardDevice> devices,
        IReadOnlyCollection<IoPointDefinition> inputs,
        IReadOnlyCollection<IoPointDefinition> outputs)
    {
        var deviceMap = new Dictionary<string, IIoCardDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (IIoCardDevice device in devices)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentException.ThrowIfNullOrWhiteSpace(device.DeviceId);
            if (!deviceMap.TryAdd(device.DeviceId, device))
                throw new ArgumentException($"Duplicate IO device ID '{device.DeviceId}'.", nameof(devices));
        }

        var allIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IoPointDefinition> diMap = ValidatePoints(inputs, IoSignalKind.DigitalInput, deviceMap, allIds, nameof(inputs));
        Dictionary<string, IoPointDefinition> doMap = ValidatePoints(outputs, IoSignalKind.DigitalOutput, deviceMap, allIds, nameof(outputs));
        return new InitializationMaps(diMap, doMap);
    }

    private static Dictionary<string, IoPointDefinition> ValidatePoints(
        IEnumerable<IoPointDefinition> points,
        IoSignalKind expectedKind,
        IReadOnlyDictionary<string, IIoCardDevice> devices,
        ISet<string> allIds,
        string parameterName)
    {
        var result = new Dictionary<string, IoPointDefinition>(StringComparer.OrdinalIgnoreCase);
        var physicalChannels = new HashSet<(string DeviceId, int Channel)>(DeviceChannelComparer.Instance);

        foreach (IoPointDefinition point in points)
        {
            ArgumentNullException.ThrowIfNull(point);
            point.Validate();
            if (point.Kind != expectedKind)
                throw new ArgumentException(
                    $"IO point '{point.Id}' is '{point.Kind}' but was placed in the '{expectedKind}' collection.",
                    parameterName);
            if (!devices.TryGetValue(point.DeviceId, out IIoCardDevice? device))
                throw new ArgumentException($"IO point '{point.Id}' references unknown device '{point.DeviceId}'.", parameterName);
            int channelCount = expectedKind == IoSignalKind.DigitalInput
                ? device.DigitalInputCount
                : device.DigitalOutputCount;
            IoChannelGuard.ValidateChannel(point.Channel, channelCount, nameof(point.Channel));
            if (!allIds.Add(point.Id))
                throw new ArgumentException($"Duplicate IO point ID '{point.Id}'.", parameterName);
            if (!physicalChannels.Add((point.DeviceId, point.Channel)))
                throw new ArgumentException($"Duplicate {expectedKind} channel '{point.DeviceId}:{point.Channel}'.", parameterName);
            result.Add(point.Id, point);
        }

        return result;
    }

    /// <summary>
    /// 统一处理轮询与硬件通知产生的物理输入快照。
    /// 在锁内更新缓存和计算变化，在锁外通知订阅者，避免业务回调阻塞状态采集。
    /// </summary>
    private void ProcessSignalSnapshot(IoSignalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string deviceId = snapshot.DeviceId;
        ulong currentMask = snapshot.Mask;
        List<(string PointId, bool State)>? changes = null;
        // 硬件通知线程和后台轮询可能并发到达，缓存更新与差异计算必须保持原子性。
        lock (_maskSync)
        {
            // 设备被重配或停止后到达的旧中断快照不应重新写入缓存。
            if (!_devices.ContainsKey(deviceId))
                return;

            bool hasPrevious = _deviceMaskCache.TryGetValue(deviceId, out ulong lastMask);
            if (!hasPrevious || currentMask != lastMask)
            {
                // 首次快照需要发布所有已配置点位，让 UI 和联锁建立确定的初始状态。
                _deviceMaskCache[deviceId] = currentMask;
                ulong diff = hasPrevious ? currentMask ^ lastMask : ulong.MaxValue;
                if (_fastReverseDiMap.TryGetValue(deviceId, out string[]? channelLabels))
                {
                    for (int i = 0; i < 64; i++)
                    {
                        if ((diff & (1UL << i)) != 0)
                        {
                            string label = channelLabels[i];
                            if (!string.IsNullOrEmpty(label) && _diMap.TryGetValue(label, out var point))
                            {
                                bool physicalState = (currentMask & (1UL << i)) != 0;
                                bool newState = physicalState ^ point.Inverted;
                                (changes ??= new()).Add((label, newState));
                            }
                        }
                    }
                }
            }
        }

        // 订阅者回调不属于掩码临界区，避免 UI 或业务回调阻塞扫描线程。
        PublishSnapshotReceived(snapshot);
        if (changes is null) return;
        foreach ((string pointId, bool state) in changes)
            PublishStateChanged(pointId, state);
    }

    private void PublishStateChanged(string pointId, bool state)
    {
        Delegate[] subscribers = OnIoStateChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
        foreach (Action<string, bool> subscriber in subscribers.Cast<Action<string, bool>>())
        {
            try
            {
                subscriber(pointId, state);
            }
            catch (Exception exception)
            {
                try { OnIoNotificationFailed?.Invoke(pointId, exception); }
                catch { }
            }
        }
    }

    private void PublishSnapshotReceived(IoSignalSnapshot snapshot)
    {
        Delegate[] subscribers = OnIoSnapshotReceived?.GetInvocationList() ?? Array.Empty<Delegate>();
        foreach (Action<IoSignalSnapshot> subscriber in subscribers.Cast<Action<IoSignalSnapshot>>())
        {
            try { subscriber(snapshot); }
            catch (Exception exception) { PublishNotificationFailure(snapshot.DeviceId, exception); }
        }
    }

    private void PublishNotificationFailure(string source, Exception exception)
    {
        foreach (Action<string, Exception> subscriber in (OnIoNotificationFailed?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<string, Exception>>())
        {
            try { subscriber(source, exception); }
            catch { }
        }
    }

    /// <summary>
    /// 后台轮询并检测已配置点位的状态变化。
    /// </summary>
    private void StartHeartbeat()
    {
        if (_scanTask is { IsCompleted: false })
        {
            return;
        }

        _scanCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        // 任务只捕获本次启动的取消源，避免 Reset 置空字段导致竞态。
        _scanTask = Task.Run(() => RunScanLoopAsync(cancellation.Token));
    }

    private async Task RunScanLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.PollingInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            ScanOnce();

            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ScanOnce()
    {
        foreach (var deviceId in _devices.Keys)
        {
            try
            {
                ulong currentMask = _devices[deviceId].ReadDiPortMask();
                ProcessSignalSnapshot(new IoSignalSnapshot(deviceId, currentMask, DateTimeOffset.UtcNow, IoSnapshotSource.Polling));
            }
            catch (Exception ex)
            {
                PublishReadFailure(deviceId, ex);
                // 保持扫描任务存活，单张卡的瞬时读取异常不应终止整个 IO 管理器。
            }
        }
    }

    // 逻辑点位读写与硬件通知等待。

    /// <summary>
    /// 等待逻辑 DI 的硬件中断通知。仅用于上位机流程协调，飞拍等确定时序必须使用控制器硬件触发。
    /// </summary>
    /// <param name="label">逻辑输入点位 ID，例如 <c>transport.fixture.present</c>。</param>
    /// <param name="expectedState">期望等到的电平状态（true=高电平，false=低电平）</param>
    /// <param name="token">取消令牌</param>
    public Task WaitForInputInterruptAsync(string label, bool expectedState, CancellationToken token = default)
    {
        if (!_diMap.TryGetValue(label, out var point))
            throw new ArgumentException($"未定义的 DI 标签: {label}");
        if (!_devices.TryGetValue(point.DeviceId, out var device))
            throw new InvalidOperationException($"IO 设备 {point.DeviceId} 未就绪");
        IoChannelGuard.ValidateChannel(point.Channel, device.DigitalInputCount, nameof(point.Channel));

        if (device is not IHardwareInterruptSource interruptSource)
        {
            throw new NotSupportedException($"IO device '{point.DeviceId}' does not provide hardware interrupt notifications.");
        }

        return WaitForHardwareInterruptCoreAsync(
            device,
            interruptSource,
            point.Channel,
            expectedState ^ point.Inverted,
            token);
    }

    private void PublishReadFailure(string deviceId, Exception exception)
    {
        foreach (Action<string, Exception> subscriber in (OnIoReadFailed?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<string, Exception>>())
        {
            try { subscriber(deviceId, exception); }
            catch (Exception notificationException) { PublishNotificationFailure(deviceId, notificationException); }
        }
    }

    private static Task WaitForHardwareInterruptCoreAsync(
        IIoCardDevice device,
        IHardwareInterruptSource interruptSource,
        int channel,
        bool expectedPhysicalState,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<IoSignalSnapshot> handler = (_, snapshot) =>
        {
            if (((snapshot.Mask & (1UL << channel)) != 0) == expectedPhysicalState)
                completion.TrySetResult();
        };

        interruptSource.HardwareInterruptReceived += handler;
        CancellationTokenRegistration registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));
        _ = completion.Task.ContinueWith(
            _ =>
            {
                interruptSource.HardwareInterruptReceived -= handler;
                registration.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            if (device.ReadDiBit(channel) == expectedPhysicalState)
                completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        return completion.Task;
    }

    public bool ReadDi(string pointId)
    {
        if (!_diMap.ContainsKey(pointId))
            throw new KeyNotFoundException($"Undefined DI point ID: {pointId}");
        if (!TryReadDi(pointId, out bool state))
            throw new InvalidOperationException($"DI point '{pointId}' has no captured device snapshot.");
        return state;
    }

    public bool TryReadDi(string pointId, out bool state)
    {
        state = false;
        if (!_diMap.TryGetValue(pointId, out IoPointDefinition? point)
            || !_deviceMaskCache.TryGetValue(point.DeviceId, out ulong mask))
            return false;

        bool physicalState = (mask & (1UL << point.Channel)) != 0;
        state = physicalState ^ point.Inverted;
        return true;
    }

    /// <summary>
    /// 按逻辑点位 ID 写入输出，并应用 Owner 与反相规则。
    /// </summary>
    public void WriteDo(string pointId, bool state, string? owner = null)
    {
        IoPointDefinition point = GetOutputForWrite(pointId, owner);
        try
        {
            WriteLogicalOutput(point, state);
        }
        catch (Exception exception)
        {
            PublishWriteFailure(point.Id, exception);
            throw;
        }
    }

    /// <summary>
    /// 写入逻辑输出的软件定时脉冲；同一输出再次触发会重新开始计时。
    /// </summary>
    public void WriteTimedPulse(string pointId, int durationMs, string? owner = null)
    {
        if (durationMs < 0)
            throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Pulse duration cannot be negative.");

        IoPointDefinition point = GetOutputForWrite(pointId, owner);
        PulseOutputScheduler scheduler = _logicalPulseSchedulers.GetOrAdd(point.Id, _ => CreateLogicalPulseScheduler(point));
        scheduler.WritePulse(0, durationMs);
    }

    /// <summary>
    /// 将所有配置了工艺安全状态的输出切换到对应逻辑值。
    /// 此操作仅收敛普通工艺输出，不替代安全继电器或安全控制器；不受业务 Owner 限制。
    /// </summary>
    public void ApplyProcessSafeOutputs()
    {
        ThrowIfDisposed();
        var failures = new List<Exception>();
        foreach (IoPointDefinition point in _doMap.Values)
        {
            if (point.ProcessSafeState is not { } safeState)
                continue;

            try
            {
                if (!_devices.TryGetValue(point.DeviceId, out IIoCardDevice? device))
                    throw new InvalidOperationException($"IO device '{point.DeviceId}' is not available.");
                device.WriteDoBit(point.Channel, safeState ^ point.Inverted);
            }
            catch (Exception ex)
            {
                PublishWriteFailure(point.Id, ex);
                failures.Add(new InvalidOperationException($"Failed to apply the process safe state for DO '{point.Id}'.", ex));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more process output states could not be applied.", failures);
    }

    private IoPointDefinition GetOutputForWrite(string pointId, string? owner)
    {
        if (!_doMap.TryGetValue(pointId, out IoPointDefinition? point))
            throw new ArgumentException($"Undefined DO point ID: {pointId}", nameof(pointId));

        if (!string.IsNullOrWhiteSpace(point.Owner)
            && !string.Equals(point.Owner, owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"DO point '{point.Id}' is owned by '{point.Owner}'.");
        }

        return point;
    }

    private void WriteLogicalOutput(IoPointDefinition point, bool state)
    {
        if (!_devices.TryGetValue(point.DeviceId, out IIoCardDevice? device))
            throw new InvalidOperationException($"IO device '{point.DeviceId}' is not available.");

        device.WriteDoBit(point.Channel, state ^ point.Inverted);
    }

    private PulseOutputScheduler CreateLogicalPulseScheduler(IoPointDefinition point)
        => new(
            (_, state) => WriteLogicalOutput(point, state),
            () => !_disposed
                && _devices.TryGetValue(point.DeviceId, out IIoCardDevice? device)
                && device.IsConnected,
            (_, exception) => PublishWriteFailure(point.Id, exception));

    private void PublishWriteFailure(string pointId, Exception exception)
    {
        foreach (Action<string, Exception> subscriber in (OnIoWriteFailed?.GetInvocationList() ?? Array.Empty<Delegate>()).Cast<Action<string, Exception>>())
        {
            try { subscriber(pointId, exception); }
            catch (Exception notificationException) { PublishNotificationFailure(pointId, notificationException); }
        }
    }

    /// <summary>
    /// 返回已采集的所有逻辑 DI 状态，不触发硬件刷新。
    /// </summary>
    public IReadOnlyDictionary<string, bool> GetCapturedDiStates()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in _diMap.Keys)
        {
            result[label] = ReadDi(label);
        }
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Reset();
    }

    public void Stop()
    {
        Reset();
    }

    private void Reset()
    {
        var scanCancellation = Interlocked.Exchange(ref _scanCancellation, null);
        scanCancellation?.Cancel();

        try
        {
            _scanTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static item => item is OperationCanceledException))
        {
        }
        finally
        {
            scanCancellation?.Dispose();
            _scanTask = null;
        }

        foreach (var pair in _interruptHandlers)
        {
            if (_devices.TryGetValue(pair.Key, out var device)
                && device is IHardwareInterruptSource interruptSource)
            {
                interruptSource.HardwareInterruptReceived -= pair.Value;
            }
        }

        _interruptHandlers.Clear();
        foreach (PulseOutputScheduler scheduler in _logicalPulseSchedulers.Values)
            scheduler.Dispose();
        _logicalPulseSchedulers.Clear();
        _devices.Clear();
        _deviceMaskCache.Clear();
        _diMap.Clear();
        _doMap.Clear();
        _fastReverseDiMap.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IoStateMonitor));
        }
    }

    private sealed record InitializationMaps(
        Dictionary<string, IoPointDefinition> DiMap,
        Dictionary<string, IoPointDefinition> DoMap);

    private sealed class DeviceChannelComparer : IEqualityComparer<(string DeviceId, int Channel)>
    {
        public static DeviceChannelComparer Instance { get; } = new();

        public bool Equals((string DeviceId, int Channel) x, (string DeviceId, int Channel) y)
            => x.Channel == y.Channel && StringComparer.OrdinalIgnoreCase.Equals(x.DeviceId, y.DeviceId);

        public int GetHashCode((string DeviceId, int Channel) value)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.DeviceId), value.Channel);
    }
}
