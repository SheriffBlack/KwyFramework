using Kwy.Device.Abstractions.IO;
using System.Collections.Concurrent;

namespace Kwy.Device.Core.IO;

/// <summary>
/// 全局 IO 管理器 (0.5 层)
/// 负责抹平物理硬件差异，实现逻辑标签到物理引脚的动态映射
/// </summary>
public sealed class IoStateMonitor : IIoStateMonitor
{
    // 存储所有的 IO 设备 (运动控制卡或专用 IO 卡)
    private readonly ConcurrentDictionary<string, IIoCardDevice> _devices = new();

    // 稳定点位 ID -> 物理点位映射。
    private readonly Dictionary<string, IoPoint> _diMap = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IoPoint> _doMap = new(StringComparer.OrdinalIgnoreCase);

    // 🚀 优化三：缓存各张卡的物理掩码，以及上一次的状态用于对比 (升级为 64 位支持)
    private readonly ConcurrentDictionary<string, ulong> _deviceMaskCache = new();
    private readonly object _maskSync = new();
    public event Action<string, bool>? OnIoStateChanged;

    private readonly Dictionary<string, string[]> _fastReverseDiMap = new();

    private CancellationTokenSource? _scanCancellation;
    private Task? _scanTask;
    private readonly ConcurrentDictionary<string, EventHandler<IoSignalSnapshot>> _interruptHandlers = new();
    private readonly ConcurrentDictionary<string, PulseOutputScheduler> _logicalPulseSchedulers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public int PollingIntervalMs { get; set; } = 5;

    public event Action<string, Exception>? OnIoReadFailed;

    public event Action<string, Exception>? OnIoWriteFailed;

    public event Action<string, Exception>? OnIoNotificationFailed;

    public event Action<IoSignalSnapshot>? OnIoSnapshotReceived;

    public IoStateMonitor()
    { }

    /// <summary>
    /// 初始化并启动高频扫描
    /// </summary>
    public void Initialize(IEnumerable<IIoCardDevice> devices, IEnumerable<IoPoint> diConfigs, IEnumerable<IoPoint> doConfigs)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(diConfigs);
        ArgumentNullException.ThrowIfNull(doConfigs);

        IIoCardDevice[] deviceItems = devices.ToArray();
        IoPoint[] diItems = diConfigs.ToArray();
        IoPoint[] doItems = doConfigs.ToArray();
        InitializationMaps maps = BuildAndValidateMaps(deviceItems, diItems, doItems);

        // 候选配置全部校验通过后再替换运行状态，避免无效配置破坏现有监视器。
        Reset();

        foreach (IIoCardDevice dev in deviceItems)
        {
            _devices[dev.DeviceId] = dev;
            // 为每张卡预分配一个数组（支持最大 64 通道）
            _fastReverseDiMap[dev.DeviceId] = new string[64];

            // 🚀 核心架构升级：订阅硬件物理中断事件。
            // 当硬件产生中断时，微秒级瞬间触发解析，同步更新内存缓存并广播 UI 状态更新事件，无需等待 5ms 轮询！
            if (dev is IHardwareInterruptSource interruptSource)
            {
                EventHandler<IoSignalSnapshot> handler = (_, snapshot) => ProcessSignalSnapshot(snapshot);
                _interruptHandlers[dev.DeviceId] = handler;
                interruptSource.HardwareInterruptReceived += handler;
            }
        }

        foreach ((string id, IoPoint point) in maps.DiMap)
        {
            _diMap[id] = point;
            _fastReverseDiMap[point.DeviceId][point.Channel] = id;
        }
        foreach ((string id, IoPoint point) in maps.DoMap)
        {
            _doMap[id] = point;
        }

        StartHeartbeat();
    }

    private static InitializationMaps BuildAndValidateMaps(
        IReadOnlyCollection<IIoCardDevice> devices,
        IReadOnlyCollection<IoPoint> inputs,
        IReadOnlyCollection<IoPoint> outputs)
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
        Dictionary<string, IoPoint> diMap = ValidatePoints(inputs, IoSignalKind.DigitalInput, deviceMap, allIds, nameof(inputs));
        Dictionary<string, IoPoint> doMap = ValidatePoints(outputs, IoSignalKind.DigitalOutput, deviceMap, allIds, nameof(outputs));
        return new InitializationMaps(diMap, doMap);
    }

    private static Dictionary<string, IoPoint> ValidatePoints(
        IEnumerable<IoPoint> points,
        IoSignalKind expectedKind,
        IReadOnlyDictionary<string, IIoCardDevice> devices,
        ISet<string> allIds,
        string parameterName)
    {
        var result = new Dictionary<string, IoPoint>(StringComparer.OrdinalIgnoreCase);
        var physicalChannels = new HashSet<(string DeviceId, int Channel)>(DeviceChannelComparer.Instance);

        foreach (IoPoint point in points)
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
    /// 统一解析引脚掩码变化，同时支持【轮询线程】和【硬件中断回调】的高效调用。
    /// 包含并发锁确保线程安全，并通过缓存对比实现自动去重。
    /// </summary>
    private void ProcessSignalSnapshot(IoSignalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string deviceId = snapshot.DeviceId;
        ulong currentMask = snapshot.Mask;
        List<(string PointId, bool State)>? changes = null;
        // 🚀 使用锁确保当硬件中断线程与 5ms 扫描线程同时触发时，状态更新与事件广播依然绝对安全且不产生竞争
        lock (_maskSync)
        {
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
    /// 核心心脏：5ms 高频扫描 + 状态变更检测
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
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(PollingIntervalMs, 1)));

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

    // ==========================================
    // 🚀 核心读写 API
    // ==========================================

    /// <summary>
    /// 【硬件中断模式】提供给极速飞拍、核心触发使用的“硬件中断等待”接口。
    /// 完全绕过 5ms 轮询，通过 TaskCompletionSource 直连板卡底层的 PCI 中断回调。
    /// 注意：如果板卡的指定通道物理上不支持中断，此方法将永远处于等待状态。
    /// </summary>
    /// <param name="label">逻辑 IO 名称，如 "DI_PLC_OK"</param>
    /// <param name="expectedState">期望等到的电平状态（true=高电平，false=低电平）</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务，达到预期状态时瞬间完成 (微秒级延迟)</returns>
    public Task WaitForHardwareInterruptAsync(string label, bool expectedState, System.Threading.CancellationToken token)
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

    /// <summary>
    /// 【硬件中断模式】直接通过物理设备和通道号等待中断（绕过逻辑名称与极性映射，直接读取物理电平）。
    /// </summary>
    /// <param name="device">物理板卡设备实例</param>
    /// <param name="channel">输入通道索引 (最高 0-63)</param>
    /// <param name="expectedState">期望的物理电平状态 (true=高电平，false=低电平)</param>
    /// <param name="token">取消令牌</param>
    /// <returns>异步任务，达到期望电平时完成</returns>
    public Task WaitForHardwareInterruptAsync(IIoCardDevice device, int channel, bool expectedState, CancellationToken token)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        IoChannelGuard.ValidateChannel(channel, device.DigitalInputCount, nameof(channel));

        if (device is not IHardwareInterruptSource interruptSource)
        {
            throw new NotSupportedException($"IO device '{device.DeviceId}' does not provide hardware interrupt notifications.");
        }

        return WaitForHardwareInterruptCoreAsync(device, interruptSource, channel, expectedState, token);
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
        if (!_diMap.TryGetValue(pointId, out IoPoint? point)
            || !_deviceMaskCache.TryGetValue(point.DeviceId, out ulong mask))
            return false;

        bool physicalState = (mask & (1UL << point.Channel)) != 0;
        state = physicalState ^ point.Inverted;
        return true;
    }

    /// <summary>
    /// 写入逻辑输出 (DO)
    /// </summary>
    public void WriteDo(string pointId, bool state, string? owner = null)
    {
        IoPoint point = GetOutputForWrite(pointId, owner);
        WriteLogicalOutput(point, state);
    }

    /// <summary>
    /// 写入逻辑输出高精度脉冲 (DO)
    /// </summary>
    public void WritePulse(string pointId, int durationMs, string? owner = null)
    {
        if (durationMs < 0)
            throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Pulse duration cannot be negative.");

        IoPoint point = GetOutputForWrite(pointId, owner);
        PulseOutputScheduler scheduler = _logicalPulseSchedulers.GetOrAdd(point.Id, _ => CreateLogicalPulseScheduler(point));
        scheduler.WritePulse(0, durationMs);
    }

    /// <summary>
    /// 将所有已配置安全状态的输出切换到逻辑安全值。
    /// 安全处置是系统级操作，不受业务 Owner 限制。
    /// </summary>
    public void ApplySafeOutputs()
    {
        ThrowIfDisposed();
        var failures = new List<Exception>();
        foreach (IoPoint point in _doMap.Values)
        {
            if (point.SafeState is not { } safeState)
                continue;

            try
            {
                if (!_devices.TryGetValue(point.DeviceId, out IIoCardDevice? device))
                    throw new InvalidOperationException($"IO device '{point.DeviceId}' is not available.");
                device.WriteDoBit(point.Channel, safeState ^ point.Inverted);
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException($"Failed to apply the safe state for DO '{point.Id}'.", ex));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more safe output states could not be applied.", failures);
    }

    private IoPoint GetOutputForWrite(string pointId, string? owner)
    {
        if (!_doMap.TryGetValue(pointId, out IoPoint? point))
            throw new ArgumentException($"Undefined DO point ID: {pointId}", nameof(pointId));

        if (!string.IsNullOrWhiteSpace(point.Owner)
            && !string.Equals(point.Owner, owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"DO point '{point.Id}' is owned by '{point.Owner}'.");
        }

        return point;
    }

    private void WriteLogicalOutput(IoPoint point, bool state)
    {
        if (!_devices.TryGetValue(point.DeviceId, out IIoCardDevice? device))
            throw new InvalidOperationException($"IO device '{point.DeviceId}' is not available.");

        device.WriteDoBit(point.Channel, state ^ point.Inverted);
    }

    private PulseOutputScheduler CreateLogicalPulseScheduler(IoPoint point)
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
    /// 批量刷新所有 DI (用于 UI 显示，性能更高)
    /// </summary>
    public IReadOnlyDictionary<string, bool> RefreshAllDi()
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
        Dictionary<string, IoPoint> DiMap,
        Dictionary<string, IoPoint> DoMap);

    private sealed class DeviceChannelComparer : IEqualityComparer<(string DeviceId, int Channel)>
    {
        public static DeviceChannelComparer Instance { get; } = new();

        public bool Equals((string DeviceId, int Channel) x, (string DeviceId, int Channel) y)
            => x.Channel == y.Channel && StringComparer.OrdinalIgnoreCase.Equals(x.DeviceId, y.DeviceId);

        public int GetHashCode((string DeviceId, int Channel) value)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.DeviceId), value.Channel);
    }
}
