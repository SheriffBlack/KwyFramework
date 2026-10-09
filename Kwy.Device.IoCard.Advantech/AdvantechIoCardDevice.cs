using Kwy.Device.IoCard.Abstractions;
using Automation.BDaq;
using Kwy.Device.IoCard.Core;
using Kwy.Device.IoCard.Advantech;

namespace Kwy.Device.IoCard.Advantech;

/// <summary>
/// Advantech PCI/DAQNavi digital IO card implementation.
/// </summary>
public sealed class AdvantechIoCardDevice : DigitalIoDeviceBase
{
    private readonly AdvantechIoCardConfig config;
    private readonly InstantDiCtrl diController = new();
    private readonly InstantDoCtrl doController = new();
    private readonly SemaphoreSlim ioSemaphore = new(1, 1);
    private readonly object interruptSync = new();
    private byte[] diPortBuffer = Array.Empty<byte>();
    private byte[] doPortBuffer = Array.Empty<byte>();
    private volatile bool connected;
    private volatile bool shuttingDown;
    private static readonly TimeSpan NativeReleaseWaitTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IoOperationWaitTimeout = TimeSpan.FromMilliseconds(100);
    private int nativeResourcesReleased;

    public AdvantechIoCardDevice(AdvantechIoCardConfig config)
        : this(config.DeviceDescription, config.Model, config)
    {
    }

    public AdvantechIoCardDevice(string deviceId, string deviceName, AdvantechIoCardConfig config)
        : base(deviceId, deviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        if (!config.Validate())
        {
            throw new ArgumentException("Invalid Advantech IO card configuration.", nameof(config));
        }
    }

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        shuttingDown = false;
        SelectDevice();

        if (config.EnableInterrupt)
        {
            ConfigureInterrupt();
            diController.Interrupt -= OnDiInterrupt;
            diController.Interrupt += OnDiInterrupt;
            try
            {
                ThrowIfFailed(diController.SnapStart(), "启动 DAQNavi DI 中断监听失败");
            }
            catch
            {
                diController.Interrupt -= OnDiInterrupt;
                throw;
            }
        }

        connected = true;
        return Task.CompletedTask;
    }

    protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        shuttingDown = true;
        connected = false;

        if (config.EnableInterrupt)
        {
            try
            {
                ThrowIfFailed(diController.SnapStop(), "停止 DAQNavi DI 中断监听失败");
            }
            catch
            {
            }

            diController.Interrupt -= OnDiInterrupt;
        }

        return Task.CompletedTask;
    }

    protected override bool IsConnectionAlive()
    {
        return connected;
    }

    public override void WriteDoBit(int channel, bool state)
    {
        EnsureReady();
        DigitalIoChannelGuard.ValidateChannel(channel, GetDoChannelCount(), nameof(channel));

        ExecuteIo(() =>
        {
            int port = channel / 8;
            int bit = channel % 8;
            ThrowIfFailed(doController.WriteBit(port, bit, state ? (byte)1 : (byte)0), "写入 DO 位失败");
        });
    }

    public override void WriteDoPortMask(ulong mask)
    {
        EnsureReady();
        WriteDoPortMaskCore(mask, GetWritableDoMask());
    }

    public override void WriteDoPortMask(ulong mask, ulong changedMask)
    {
        EnsureReady();
        WriteDoPortMaskCore(mask, changedMask & GetWritableDoMask());
    }

    public override bool ReadDiBit(int channel)
    {
        EnsureReady();
        DigitalIoChannelGuard.ValidateChannel(channel, GetDiChannelCount(), nameof(channel));

        return ExecuteIo(() =>
        {
            int port = channel / 8;
            int bit = channel % 8;
            ThrowIfFailed(diController.ReadBit(port, bit, out byte value), "读取 DI 位失败");
            return value != 0;
        });
    }

    public override bool[] ReadAllDi()
    {
        EnsureReady();
        return ReadDiPorts(GetDiPortCount());
    }

    public override bool[] ReadAllDo()
    {
        EnsureReady();
        return ReadDoPorts(GetDoPortCount());
    }

    public override ulong ReadDiPortMask()
    {
        EnsureReady();
        return ReadDiPortMaskCore();
    }

    public override async ValueTask DisposeAsync()
    {
        if (disposed && Volatile.Read(ref nativeResourcesReleased) != 0)
        {
            return;
        }

        shuttingDown = true;
        connected = false;

        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            if (State == Kwy.Communicate.Abstractions.Enums.ConnectionState.Disconnected)
                ReleaseNativeResources();
            if (Volatile.Read(ref nativeResourcesReleased) != 0)
                GC.SuppressFinalize(this);
        }
    }
    public override void Dispose()
        => DisposeAsync().AsTask().GetAwaiter().GetResult();

    ~AdvantechIoCardDevice()
    {
        ReleaseNativeResources();
    }

    private void ReleaseNativeResources()
    {
        if (Volatile.Read(ref nativeResourcesReleased) != 0)
        {
            return;
        }

        connected = false;
        shuttingDown = true;

        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(NativeReleaseWaitTimeout);
            if (!lockTaken)
                return;
            if (Interlocked.Exchange(ref nativeResourcesReleased, 1) != 0)
                return;
            ReleaseNativeResourcesCore();
        }
        catch (ObjectDisposedException)
        {
            if (Interlocked.Exchange(ref nativeResourcesReleased, 1) == 0)
                ReleaseNativeResourcesCore();
        }
        finally
        {
            if (lockTaken)
            {
                try
                {
                    ioSemaphore.Release();
                }
                catch
                {
                }

                try
                {
                    ioSemaphore.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    private void ReleaseNativeResourcesCore()
    {
        try
        {
            diController.Interrupt -= OnDiInterrupt;
        }
        catch
        {
        }

        if (config.EnableInterrupt)
        {
            try
            {
                ThrowIfFailed(diController.SnapStop(), "停止 DAQNavi DI 中断监听失败");
            }
            catch
            {
            }
        }

        try
        {
            diController.Cleanup();
            doController.Cleanup();
            diController.Dispose();
            doController.Dispose();
        }
        catch
        {
        }
    }

    private ulong ReadDiPortMaskCore()
    {
        return ExecuteIo(() =>
        {
            int portCount = GetDiPortCount();
            byte[] portData = GetDiPortBuffer(portCount);
            ThrowIfFailed(diController.Read(0, portCount, portData), "读取 DI 端口失败");

            return DigitalIoBitConverter.ToMask(portData);
        });
    }

    private void ConfigureInterrupt()
    {
        lock (interruptSync)
        {
            DigitalIoChannelGuard.ValidateChannel(config.InterruptChannel, GetDiChannelCount(), nameof(config.InterruptChannel));
            int interruptIndex = config.InterruptChannel / 8;
            if (interruptIndex >= diController.Features.PortCount)
            {
                throw new NotSupportedException($"Advantech interrupt channel {config.InterruptChannel} is not supported by this device.");
            }

            if ((uint)interruptIndex >= (uint)diController.DiintChannels.Count())
            {
                throw new NotSupportedException($"Advantech interrupt channel {config.InterruptChannel} is not supported by this device.");
            }

            diController.DiintChannels[interruptIndex].Enabled = true;
            diController.DiintChannels[interruptIndex].TrigEdge = config.InterruptRisingEdge
                ? ActiveSignal.RisingEdge
                : ActiveSignal.FallingEdge;
        }
    }

    private bool[] ReadDiPorts(int portCount)
    {
        return ExecuteIo(() =>
        {
            byte[] portData = GetDiPortBuffer(portCount);
            ThrowIfFailed(diController.Read(0, portCount, portData), "读取 DI 端口失败");
            return DigitalIoBitConverter.ToBits(portData);
        });
    }

    private bool[] ReadDoPorts(int portCount)
    {
        return ExecuteIo(() =>
        {
            byte[] portData = GetDoPortBuffer(portCount);
            ThrowIfFailed(doController.Read(0, portCount, portData), "读取 DO 端口失败");
            return DigitalIoBitConverter.ToBits(portData);
        });
    }

    private void WriteDoPortMaskCore(ulong mask, ulong changedMask)
    {
        if (changedMask == 0)
        {
            return;
        }

        ExecuteIo(() =>
        {
            int portCount = GetDoPortCount();
            byte[] currentData = GetDoPortBuffer(portCount);
            ThrowIfFailed(doController.Read(0, portCount, currentData), "读取 DO 端口失败");

            for (int port = 0; port < portCount; port++)
            {
                byte targetValue = currentData[port];
                for (int bit = 0; bit < 8; bit++)
                {
                    int channel = port * 8 + bit;
                    ulong bitMask = 1UL << channel;
                    if ((changedMask & bitMask) == 0)
                    {
                        continue;
                    }

                    if ((mask & bitMask) != 0)
                    {
                        targetValue = (byte)(targetValue | (1 << bit));
                    }
                    else
                    {
                        targetValue = (byte)(targetValue & ~(1 << bit));
                    }
                }

                if (targetValue != currentData[port])
                {
                    ThrowIfFailed(doController.Write(port, targetValue), "写入 DO 端口失败");
                }
            }
        });
    }

    private byte[] GetDiPortBuffer(int portCount)
    {
        if (diPortBuffer.Length != portCount)
        {
            diPortBuffer = new byte[portCount];
        }

        return diPortBuffer;
    }

    private byte[] GetDoPortBuffer(int portCount)
    {
        if (doPortBuffer.Length != portCount)
        {
            doPortBuffer = new byte[portCount];
        }

        return doPortBuffer;
    }

    private T ExecuteIo<T>(Func<T> operation)
    {
        ThrowIfUnavailable();
        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(IoOperationWaitTimeout);
            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for Advantech IO operation lock.");
            }

            ThrowIfUnavailable();
            return operation();
        }
        catch (Exception exception)
        {
            RaiseErrorOccurred($"[{DeviceName}/{DeviceId}] Advantech IO operation failed: {exception.Message}", exception);
            throw;
        }
        finally
        {
            if (lockTaken)
            {
                ioSemaphore.Release();
            }
        }
    }
    private void ExecuteIo(Action operation)
    {
        ThrowIfUnavailable();
        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(IoOperationWaitTimeout);
            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for Advantech IO operation lock.");
            }

            ThrowIfUnavailable();
            operation();
        }
        catch (Exception exception)
        {
            RaiseErrorOccurred($"[{DeviceName}/{DeviceId}] Advantech IO operation failed: {exception.Message}", exception);
            throw;
        }
        finally
        {
            if (lockTaken)
            {
                ioSemaphore.Release();
            }
        }
    }
    private void OnDiInterrupt(object? sender, DiSnapEventArgs eventArgs)
    {
        if (eventArgs.SrcNum == config.InterruptChannel / 8)
        {
            ThreadPool.QueueUserWorkItem(_ => PublishHardwareTriggerSnapshot());
        }
    }

    private void PublishHardwareTriggerSnapshot()
    {
        try
        {
            RaiseHardwareInterrupt(
                ReadDiPortMaskCore(),
                config.InterruptRisingEdge ? DigitalInputTriggerEdge.Rising : DigitalInputTriggerEdge.Falling);
        }
        catch (Exception ex)
        {
            if (!shuttingDown)
            {
                RaiseErrorOccurred($"Read Advantech DI snapshot after interrupt failed: {ex.Message}", ex);
            }
        }
    }

    private int GetDiPortCount()
    {
        return Math.Min(config.DiPortCount, Math.Min(AdvantechIoCardConfig.MaxSupportedPorts, diController.Features.PortCount));
    }

    private int GetDoPortCount()
    {
        return Math.Min(config.DoPortCount, Math.Min(AdvantechIoCardConfig.MaxSupportedPorts, doController.Features.PortCount));
    }

    private int GetDiChannelCount()
    {
        return GetDiPortCount() * 8;
    }

    private int GetDoChannelCount()
    {
        return GetDoPortCount() * 8;
    }

    private ulong GetWritableDoMask()
    {
        return DigitalIoBitConverter.CreateWritableMask(GetDoChannelCount());
    }

    protected override int GetDigitalOutputChannelCount()
    {
        return GetDoChannelCount();
    }

    protected override int GetDigitalInputChannelCount()
    {
        return GetDiChannelCount();
    }

    private void EnsureReady()
    {
        ThrowIfDisposed();
        ThrowIfUnavailable();
    }

    private void ThrowIfUnavailable()
    {
        if (shuttingDown || !IsConnected)
        {
            throw new InvalidOperationException("Advantech IO card is not connected.");
        }
    }

    private void SelectDevice()
    {
        var device = new DeviceInformation(config.DeviceDescription);
        diController.SelectedDevice = device;
        doController.SelectedDevice = device;
    }

    private static void ThrowIfFailed(ErrorCode errorCode, string operation)
    {
        if (errorCode != ErrorCode.Success)
        {
            throw new InvalidOperationException($"DAQNavi {operation}: {errorCode}。");
        }
    }

}
