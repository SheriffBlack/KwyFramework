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

    // 逻辑名 -> 物理点位映射表
    private readonly Dictionary<string, IoPoint> _diMap = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IoPoint> _doMap = new(StringComparer.OrdinalIgnoreCase);

    // 🚀 优化三：缓存各张卡的物理掩码，以及上一次的状态用于对比 (升级为 64 位支持)
    private readonly ConcurrentDictionary<string, ulong> _deviceMaskCache = new();

    private readonly ConcurrentDictionary<string, ulong> _previousMaskCache = new();

    public event Action<string, bool>? OnIoStateChanged;

    private readonly Dictionary<string, string[]> _fastReverseDiMap = new();

    private CancellationTokenSource? _scanCancellation;
    private Task? _scanTask;
    private readonly ConcurrentDictionary<string, EventHandler<ulong>> _interruptHandlers = new();
    private bool _disposed;

    public int PollingIntervalMs { get; set; } = 5;

    public event Action<string, Exception>? OnIoReadFailed;

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

        Reset();

        foreach (var dev in devices)
        {
            if (dev == null)
            {
                continue;
            }

            _devices[dev.DeviceId] = dev;
            _deviceMaskCache[dev.DeviceId] = 0;
            _previousMaskCache[dev.DeviceId] = 0;

            // 为每张卡预分配一个数组（支持最大 64 通道）
            _fastReverseDiMap[dev.DeviceId] = new string[64];

            // 🚀 核心架构升级：订阅硬件物理中断事件。
            // 当硬件产生中断时，微秒级瞬间触发解析，同步更新内存缓存并广播 UI 状态更新事件，无需等待 5ms 轮询！
            if (dev is IHardwareInterruptSource interruptSource)
            {
                EventHandler<ulong> handler = (sender, mask) => ProcessMaskChange(dev.DeviceId, mask);
                _interruptHandlers[dev.DeviceId] = handler;
                interruptSource.OnHardwareTriggerReceived += handler;
            }
        }

        foreach (var di in diConfigs)
        {
            IoChannelGuard.ValidateChannel(di.Channel, IoChannelGuard.MaxChannelCount, nameof(di.Channel));
            _diMap[di.Name] = di;

            // 预先将逻辑标签填入数组对应的槽位中
            if (_fastReverseDiMap.TryGetValue(di.DeviceId, out var channelArray) && di.Channel < channelArray.Length)
            {
                channelArray[di.Channel] = di.Name;
            }
        }
        foreach (var @do in doConfigs)
        {
            IoChannelGuard.ValidateChannel(@do.Channel, IoChannelGuard.MaxChannelCount, nameof(@do.Channel));
            _doMap[@do.Name] = @do;
        }

        StartHeartbeat();
    }

    /// <summary>
    /// 统一解析引脚掩码变化，同时支持【轮询线程】和【硬件中断回调】的高效调用。
    /// 包含并发锁确保线程安全，并通过缓存对比实现自动去重。
    /// </summary>
    private void ProcessMaskChange(string deviceId, ulong currentMask)
    {
        // 🚀 使用锁确保当硬件中断线程与 5ms 扫描线程同时触发时，状态更新与事件广播依然绝对安全且不产生竞争
        lock (_previousMaskCache)
        {
            ulong lastMask = _previousMaskCache.TryGetValue(deviceId, out var m) ? m : (ulong)0;
            if (currentMask == lastMask) return; // 如果状态没变，或者已经被中断处理过了，直接退出

            // 1. 同步刷新状态缓存（O(1) 极速存取）
            _deviceMaskCache[deviceId] = currentMask;
            _previousMaskCache[deviceId] = currentMask;

            // 2. 差异检测 (XOR 异或)
            ulong diff = currentMask ^ lastMask;

            // 3. 拿到该设备通道逻辑标签的反向高速查找映射表
            string[] channelLabels = _fastReverseDiMap[deviceId];

            // 4. O(1) 遍历发生变化的引脚，最高支持 64 通道
            for (int i = 0; i < 64; i++)
            {
                if ((diff & (1UL << i)) != 0)
                {
                    string label = channelLabels[i];
                    if (!string.IsNullOrEmpty(label) && _diMap.TryGetValue(label, out var point))
                    {
                        bool physicalState = (currentMask & (1UL << i)) != 0;
                        bool newState = physicalState ^ point.Inverted;

                        // 广播状态变更通知，诊断 UI 界面（DiViewModel）将立刻同步接收
                        OnIoStateChanged?.Invoke(label, newState);
                    }
                }
            }
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
        _scanCancellation = new CancellationTokenSource();
        _scanTask = Task.Run(() => RunScanLoopAsync(_scanCancellation.Token));
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
                ProcessMaskChange(deviceId, currentMask);
            }
            catch (Exception ex)
            {
                OnIoReadFailed?.Invoke(deviceId, ex);
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
        IoChannelGuard.ValidateChannel(point.Channel, IoChannelGuard.MaxChannelCount, nameof(point.Channel));

        if (!_devices.TryGetValue(point.DeviceId, out var device))
            throw new InvalidOperationException($"IO 设备 {point.DeviceId} 未就绪");

        if (device is not IHardwareInterruptSource interruptSource)
        {
            throw new NotSupportedException($"IO device '{point.DeviceId}' does not provide hardware interrupt notifications.");
        }

        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ulong>? handler = null;
        CancellationTokenRegistration reg = default;
        handler = (sender, mask) =>
        {
            // 收到中断快照的瞬间，解析对应通道的物理电平，并带上极性反转逻辑计算逻辑电平
            bool physicalState = (mask & (1UL << point.Channel)) != 0;
            bool logicalState = physicalState ^ point.Inverted;

            if (logicalState == expectedState)
            {
                tcs.TrySetResult(true);
                interruptSource.OnHardwareTriggerReceived -= handler;
                reg.Dispose();
            }
        };

        interruptSource.OnHardwareTriggerReceived += handler;
        reg = token.Register(() =>
        {
            interruptSource.OnHardwareTriggerReceived -= handler;
            tcs.TrySetCanceled(token);
        });

        // 【防御性编程】在挂载中断事件的瞬间，有可能信号已经跳变完成了。
        // 为了防止漏掉前置跳变导致“死等”，我们在这里补一刀：注册完后主动探测一次电平。
        // （直接从底层硬件读，不从缓存读，确保绝对实时）
        bool currentPhysical = device.ReadDiBit(point.Channel);
        if ((currentPhysical ^ point.Inverted) == expectedState)
        {
            interruptSource.OnHardwareTriggerReceived -= handler;
            reg.Dispose();
            return Task.CompletedTask;
        }

        return tcs.Task;
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
        IoChannelGuard.ValidateChannel(channel, IoChannelGuard.MaxChannelCount, nameof(channel));

        if (device is not IHardwareInterruptSource interruptSource)
        {
            throw new NotSupportedException($"IO device '{device.DeviceId}' does not provide hardware interrupt notifications.");
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ulong>? handler = null;
        CancellationTokenRegistration reg = default;
        handler = (sender, mask) =>
        {
            // 收到跳变快照，直接位运算判定对应的物理通道
            bool physicalState = (mask & (1UL << channel)) != 0;
            if (physicalState == expectedState)
            {
                tcs.TrySetResult(true);
                interruptSource.OnHardwareTriggerReceived -= handler;
                reg.Dispose();
            }
        };

        interruptSource.OnHardwareTriggerReceived += handler;
        reg = token.Register(() =>
        {
            interruptSource.OnHardwareTriggerReceived -= handler;
            tcs.TrySetCanceled(token);
        });

        // 防御性安全自检：如果注册时就已经跳变到位，立刻返回
        if (device.ReadDiBit(channel) == expectedState)
        {
            interruptSource.OnHardwareTriggerReceived -= handler;
            reg.Dispose();
            return Task.CompletedTask;
        }

        return tcs.Task;
    }

    public bool ReadDi(string label)
    {
        if (!_diMap.TryGetValue(label, out var point)) return false;

        // 从内存缓存中读取，性能极高
        if (_deviceMaskCache.TryGetValue(point.DeviceId, out ulong mask))
        {
            bool physicalState = (mask & (1UL << point.Channel)) != 0;
            return physicalState ^ point.Inverted;
        }
        return false;
    }

    /// <summary>
    /// 写入逻辑输出 (DO)
    /// </summary>
    public void WriteDo(string label, bool state)
    {
        if (!_doMap.TryGetValue(label, out var point))
            throw new ArgumentException($"未定义的 DO 标签: {label}");
        IoChannelGuard.ValidateChannel(point.Channel, IoChannelGuard.MaxChannelCount, nameof(point.Channel));

        if (!_devices.TryGetValue(point.DeviceId, out var device))
            throw new InvalidOperationException($"IO 设备 {point.DeviceId} 未就绪");

        // 核心逻辑：物理写入值 = 状态 ^ 极性反转
        device.WriteDoBit(point.Channel, state ^ point.Inverted);
    }

    /// <summary>
    /// 写入逻辑输出高精度脉冲 (DO)
    /// </summary>
    public void WritePulse(string label, int durationMs)
    {
        if (durationMs < 0)
            throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Pulse duration cannot be negative.");

        if (!_doMap.TryGetValue(label, out var point))
            throw new ArgumentException($"未定义的 DO 标签: {label}");
        IoChannelGuard.ValidateChannel(point.Channel, IoChannelGuard.MaxChannelCount, nameof(point.Channel));

        if (!_devices.TryGetValue(point.DeviceId, out var device))
            throw new InvalidOperationException($"IO 设备 {point.DeviceId} 未就绪");

        device.WriteDoBit(point.Channel, true ^ point.Inverted);
        _ = ResetLogicalPulseAsync(device, point, durationMs);
    }

    private static async Task ResetLogicalPulseAsync(IIoCardDevice device, IoPoint point, int durationMs)
    {
        try
        {
            await Task.Delay(durationMs).ConfigureAwait(false);
            if (device.IsConnected)
            {
                device.WriteDoBit(point.Channel, false ^ point.Inverted);
            }
        }
        catch
        {
            // Pulse reset is a best-effort background operation for the legacy synchronous API.
        }
    }

    /// <summary>
    /// 批量刷新所有 DI (用于 UI 显示，性能更高)
    /// </summary>
    public Dictionary<string, bool> RefreshAllDi()
    {
        var result = new Dictionary<string, bool>();
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
                interruptSource.OnHardwareTriggerReceived -= pair.Value;
            }
        }

        _interruptHandlers.Clear();
        _devices.Clear();
        _deviceMaskCache.Clear();
        _previousMaskCache.Clear();
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
}
