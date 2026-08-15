using Kwy.Device.Abstractions;
using Kwy.Device.Core;
using KwyTemplate.Contracts.Services;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.Device.Devices;

public sealed class DeviceStartupConnector : IDeviceStartupConnector
{
    private readonly IDeviceRegistry registry;
    private readonly StartupProgressService startupProgress;
    private readonly ILocalizationService localizationService;
    private readonly SemaphoreSlim connectGate = new(1, 1);
    private bool disposed;

    public DeviceStartupConnector(IDeviceRegistry registry, StartupProgressService startupProgress, ILocalizationService localizationService)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.startupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IDevice[] devices = registry.Devices.ToArray();
            if (devices.Length == 0)
            {
                startupProgress.Report(localizationService.T("Startup.Device.None", "No devices are configured for connection."), 75);
                return;
            }

            for (int index = 0; index < devices.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IDevice device = devices[index];
                double progress = 15 + index * 60d / devices.Length;
                double completedProgress = 15 + (index + 1) * 60d / devices.Length;
                startupProgress.Report(localizationService.TF("Startup.Device.Connecting", "Connecting device: {0}", device.DeviceName), progress);

                try
                {
                    if (!device.IsConnected)
                    {
                        await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (device.IsConnected)
                    {
                        startupProgress.Report(localizationService.TF("Startup.Device.Connected", "Device connected: {0}", device.DeviceName), completedProgress);
                    }
                    else
                    {
                        startupProgress.Report(localizationService.TF("Startup.Device.ConnectionAbnormal", "Device connection abnormal: {0}, State={1}, IsConnected=False", device.DeviceName, device.State), completedProgress);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    startupProgress.Report(localizationService.TF("Startup.Device.ConnectionFailed", "Device connection failed: {0}, {1}", device.DeviceName, ex.Message), completedProgress);
                }
            }

            startupProgress.Report(localizationService.T("Startup.Device.CommunicationReady", "Device communication is ready."), 75);
        }
        finally
        {
            connectGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        connectGate.Dispose();
        registry.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        connectGate.Dispose();
        await registry.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(DeviceStartupConnector));
        }
    }

}
