using Automation.BDaq;
using Kwy.Device.Core.IO;

namespace Kwy.Device.IoCards.Advantech;

/// <summary>
/// Advantech PCI/DAQNavi digital IO card implementation.
/// </summary>
public sealed class AdvantechIoCardDevice : IoCardBase
{
    private readonly AdvantechIoCardConfig config;
    private readonly InstantDiCtrl instantDiCtrl;
    private readonly InstantDoCtrl instantDoCtrl;
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

        instantDiCtrl = new InstantDiCtrl();
        instantDoCtrl = new InstantDoCtrl();
    }

    public override string DeviceModel => config.Model;

    protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            
            shuttingDown = false;
            var deviceInformation = new DeviceInformation(config.DeviceDescription);
            instantDiCtrl.SelectedDevice = deviceInformation;
            instantDoCtrl.SelectedDevice = deviceInformation;

            if (config.EnableInterrupt)
            {
                ConfigureInterrupt();
                instantDiCtrl.Interrupt -= OnInstantDiInterrupt;
                instantDiCtrl.Interrupt += OnInstantDiInterrupt;

                var error = instantDiCtrl.SnapStart();
                try
                {
                    ThrowIfFailed(error, "Start Advantech DI interrupt listener failed");
                }
                catch
                {
                    instantDiCtrl.Interrupt -= OnInstantDiInterrupt;
                    throw;
                }
            }

            connected = true;
            return Task.CompletedTask;
        }
        catch (DaqException ex)
        {
            throw CreateDaqException("Connect Advantech IO card failed", ex);
        }
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
                instantDiCtrl.SnapStop();
            }
            catch
            {
            }

            instantDiCtrl.Interrupt -= OnInstantDiInterrupt;
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
        IoChannelGuard.ValidateChannel(channel, GetDoChannelCount(), nameof(channel));

        ExecuteIo(() =>
        {
            int port = channel / 8;
            int bit = channel % 8;
            var error = instantDoCtrl.WriteBit(port, bit, state ? (byte)1 : (byte)0);
            ThrowIfFailed(error, $"Write DO channel {channel} failed");
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
        IoChannelGuard.ValidateChannel(channel, GetDiChannelCount(), nameof(channel));

        return ExecuteIo(() =>
        {
            int port = channel / 8;
            int bit = channel % 8;
            var error = instantDiCtrl.ReadBit(port, bit, out byte value);
            ThrowIfFailed(error, $"Read DI channel {channel} failed");
            return value != 0;
        });
    }

    public override bool[] ReadAllDi()
    {
        EnsureReady();
        return ReadPorts(instantDiCtrl, GetDiPortCount());
    }

    public override bool[] ReadAllDo()
    {
        EnsureReady();
        return ReadPorts(instantDoCtrl, GetDoPortCount());
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
        ReleaseNativeResources();

        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
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
        if (Interlocked.Exchange(ref nativeResourcesReleased, 1) != 0)
        {
            return;
        }

        connected = false;
        shuttingDown = true;

        bool lockTaken = false;
        try
        {
            lockTaken = ioSemaphore.Wait(NativeReleaseWaitTimeout);
            ReleaseNativeResourcesCore();
        }
        catch (ObjectDisposedException)
        {
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
            instantDiCtrl.Interrupt -= OnInstantDiInterrupt;
        }
        catch
        {
        }

        if (config.EnableInterrupt)
        {
            try
            {
                instantDiCtrl.SnapStop();
            }
            catch
            {
            }
        }

        try
        {
            instantDiCtrl.Cleanup();
        }
        catch
        {
        }

        try
        {
            instantDoCtrl.Cleanup();
        }
        catch
        {
        }

        try
        {
            instantDiCtrl.Dispose();
        }
        catch
        {
        }

        try
        {
            instantDoCtrl.Dispose();
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
            var error = instantDiCtrl.Read(0, portCount, portData);
            ThrowIfFailed(error, "Read DI port mask failed");

            return IoBitConverter.ToMask(portData);
        });
    }

    private void ConfigureInterrupt()
    {
        lock (interruptSync)
        {
            IoChannelGuard.ValidateChannel(config.InterruptChannel, GetDiChannelCount(), nameof(config.InterruptChannel));
            int interruptIndex = config.InterruptChannel / 8;
            if (interruptIndex >= instantDiCtrl.DiintChannels.Count())
            {
                throw new NotSupportedException($"Advantech interrupt channel {config.InterruptChannel} is not supported by this device.");
            }

            instantDiCtrl.DiintChannels[interruptIndex].Enabled = true;
            instantDiCtrl.DiintChannels[interruptIndex].TrigEdge = config.InterruptRisingEdge
                ? ActiveSignal.RisingEdge
                : ActiveSignal.FallingEdge;
        }
    }

    private bool[] ReadPorts(InstantDiCtrl controller, int portCount)
    {
        return ExecuteIo(() =>
        {
            byte[] portData = GetDiPortBuffer(portCount);
            var error = controller.Read(0, portCount, portData);
            ThrowIfFailed(error, "Read DI ports failed");
            return IoBitConverter.ToBits(portData);
        });
    }

    private bool[] ReadPorts(InstantDoCtrl controller, int portCount)
    {
        return ExecuteIo(() =>
        {
            byte[] portData = GetDoPortBuffer(portCount);
            var error = controller.Read(0, portCount, portData);
            ThrowIfFailed(error, "Read DO ports failed");
            return IoBitConverter.ToBits(portData);
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
            var readError = instantDoCtrl.Read(0, portCount, currentData);
            ThrowIfFailed(readError, "Read current DO ports before mask write failed");

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
                    var writeError = instantDoCtrl.Write(port, targetValue);
                    ThrowIfFailed(writeError, $"Write DO port {port} failed");
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
        catch (DaqException ex)
        {
            throw CreateDaqException("Execute Advantech IO operation failed", ex);
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
        catch (DaqException ex)
        {
            throw CreateDaqException("Execute Advantech IO operation failed", ex);
        }
        finally
        {
            if (lockTaken)
            {
                ioSemaphore.Release();
            }
        }
    }
    private void OnInstantDiInterrupt(object? sender, DiSnapEventArgs e)
    {
        if (e.SrcNum == config.InterruptChannel / 8)
        {
            ThreadPool.QueueUserWorkItem(_ => PublishHardwareTriggerSnapshot());
        }
    }

    private void PublishHardwareTriggerSnapshot()
    {
        try
        {
            RaiseHardwareTrigger(ReadDiPortMaskCore());
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
        return Math.Min(config.DiPortCount, Math.Min(AdvantechIoCardConfig.MaxSupportedPorts, instantDiCtrl.Features.PortCount));
    }

    private int GetDoPortCount()
    {
        return Math.Min(config.DoPortCount, Math.Min(AdvantechIoCardConfig.MaxSupportedPorts, instantDoCtrl.Features.PortCount));
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
        return IoBitConverter.CreateWritableMask(GetDoChannelCount());
    }

    protected override int GetDigitalOutputChannelCount()
    {
        return GetDoChannelCount();
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

    private void ThrowIfFailed(ErrorCode errorCode, string message)
    {
        if (errorCode != ErrorCode.Success)
        {
            var fullMessage = $"[{DeviceName}/{DeviceId}] {message}. DeviceDescription={config.DeviceDescription}, Model={config.Model}, ErrorCode={errorCode}.";
            RaiseErrorOccurred(fullMessage);
            throw new InvalidOperationException(fullMessage);
        }
    }

    private InvalidOperationException CreateDaqException(string operation, DaqException exception)
    {
        var fullMessage = $"[{DeviceName}/{DeviceId}] {operation}. DeviceDescription={config.DeviceDescription}, Model={config.Model}. {exception.Message}";
        RaiseErrorOccurred(fullMessage, exception);
        return new InvalidOperationException(fullMessage, exception);
    }
}
