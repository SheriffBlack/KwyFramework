using Kwy.Device.Abstractions;
using Kwy.Device.Core;
using KwyTemplate.Contracts.Services;

namespace KwyTemplate.Device.Devices;

public sealed class DeviceStartupConnector : IDeviceStartupConnector
{
    private readonly IDeviceRegistry registry;
    private readonly StartupProgressService startupProgress;
    private readonly SemaphoreSlim connectGate = new(1, 1);
    private bool disposed;

    public DeviceStartupConnector(IDeviceRegistry registry, StartupProgressService startupProgress)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.startupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
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
                startupProgress.Report("未配置需要连接的设备", 75);
                return;
            }

            for (int index = 0; index < devices.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IDevice device = devices[index];
                double progress = 15 + index * 60d / devices.Length;
                double completedProgress = 15 + (index + 1) * 60d / devices.Length;
                startupProgress.Report($"正在连接设备：{device.DeviceName}", progress);

                try
                {
                    if (!device.IsConnected)
                    {
                        await device.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (device.IsConnected)
                    {
                        startupProgress.Report($"设备连接完成：{device.DeviceName}", completedProgress);
                    }
                    else
                    {
                        startupProgress.Report($"设备连接异常：{device.DeviceName}，State={device.State}，IsConnected=False", completedProgress);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    startupProgress.Report($"设备连接失败：{device.DeviceName}，{ex.Message}", completedProgress);
                }
            }

            startupProgress.Report("设备通讯链路建立完成", 75);
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
