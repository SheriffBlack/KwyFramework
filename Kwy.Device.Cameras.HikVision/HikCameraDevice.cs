using Kwy.Device.Abstractions.Vision;
using Kwy.Device.Core.Vision;
using MvCameraControl;
using System.Runtime.ExceptionServices;

namespace Kwy.Device.Cameras.HikVision;

/// <summary>HikVision camera based on the MvCameraControl V2 SDK.</summary>
public sealed class HikCameraDevice : CameraBase, ISoftwareTriggerCamera, ICameraParameterController
{
    private readonly HikCameraConfig config;
    private MvCameraControl.IDevice? hikDevice;
    private CancellationTokenSource? receiveCancellation;
    private Task? receiveTask;

    public HikCameraDevice(HikCameraConfig config)
        : base(config.DeviceId, config.DeviceName, config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        config.ValidateAndThrow();
    }

    public override string DeviceModel => "HIKVISION_CAMERA";

    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await Task.Run(OpenDevice, cancellationToken).ConfigureAwait(false);
            await ApplyConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            CloseDeviceNoThrow();
            throw;
        }
    }

    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        MvCameraControl.IDevice? device = hikDevice;
        if (device == null)
        {
            return;
        }

        try
        {
            await StopGrabbingAsync(cancellationToken).ConfigureAwait(false);
            ThrowIfFailed(device.Close(), "Close HikVision camera failed");
        }
        finally
        {
            device.Dispose();
            hikDevice = null;
        }
    }

    protected override bool IsConnectionAlive() => hikDevice?.IsConnected == true;

    protected override Task StartGrabbingCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MvCameraControl.IDevice device = GetDevice();

        ThrowIfFailed(
            device.StreamGrabber.SetImageNodeNum((uint)config.FrameBufferCount),
            "Configure HikVision SDK frame buffer failed");
        ThrowIfFailed(device.StreamGrabber.StartGrabbing(), "Start HikVision acquisition failed");

        receiveCancellation = new CancellationTokenSource();
        receiveTask = Task.Factory.StartNew(
            () => ReceiveFrames(receiveCancellation.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        return Task.CompletedTask;
    }

    protected override async Task StopGrabbingCoreAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cancellation = receiveCancellation;
        Task? task = receiveTask;
        receiveCancellation = null;
        receiveTask = null;

        cancellation?.Cancel();
        Exception? stopException = null;
        try
        {
            MvCameraControl.IDevice? device = hikDevice;
            if (device != null)
            {
                ThrowIfFailed(device.StreamGrabber.StopGrabbing(), "Stop HikVision acquisition failed");
            }
        }
        catch (Exception ex)
        {
            stopException = ex;
        }

        try
        {
            if (task != null)
            {
                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            cancellation?.Dispose();
        }

        if (stopException != null)
        {
            ExceptionDispatchInfo.Capture(stopException).Throw();
        }
    }

    public Task ExecuteSoftwareTriggerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsGrabbing)
        {
            throw new InvalidOperationException("Start acquisition before executing a software trigger.");
        }

        MvCameraControl.IDevice device = GetDevice();
        SetEnum(device, "TriggerMode", "On");
        SetEnum(device, "TriggerSource", "Software");
        ThrowIfFailed(device.Parameters.SetCommandValue("TriggerSoftware"), "Execute HikVision software trigger failed");
        return Task.CompletedTask;
    }

    public Task SetExposureTimeAsync(double exposureTimeUs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePositiveFinite(exposureTimeUs, nameof(exposureTimeUs));
        MvCameraControl.IDevice device = GetDevice();
        SetEnum(device, "ExposureAuto", "Off");
        SetFloat(device, "ExposureTime", exposureTimeUs);
        config.ExposureTimeUs = exposureTimeUs;
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double gain, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(gain) || gain < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gain));
        }

        MvCameraControl.IDevice device = GetDevice();
        SetEnum(device, "GainAuto", "Off");
        SetFloat(device, "Gain", gain);
        config.Gain = gain;
        return Task.CompletedTask;
    }

    public override Task ApplyConfigurationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        config.ValidateAndThrow();
        MvCameraControl.IDevice device = GetDevice();

        SetEnum(device, "TriggerMode", config.TriggerModeEnabled ? "On" : "Off");
        if (config.TriggerModeEnabled)
        {
            SetEnum(device, "TriggerSource", ToHikTriggerSource(config.TriggerSource));
        }

        SetEnum(device, "ExposureAuto", "Off");
        SetFloat(device, "ExposureTime", config.ExposureTimeUs);
        SetEnum(device, "GainAuto", "Off");
        SetFloat(device, "Gain", config.Gain);

        if (!string.IsNullOrWhiteSpace(config.PixelFormat))
        {
            SetEnum(device, "PixelFormat", config.PixelFormat);
        }

        if (config.AcquisitionFrameRate is double frameRate)
        {
            ThrowIfFailed(
                device.Parameters.SetBoolValue("AcquisitionFrameRateEnable", true),
                "Enable HikVision acquisition frame rate failed");
            SetFloat(device, "AcquisitionFrameRate", frameRate);
        }

        return Task.CompletedTask;
    }

    private void OpenDevice()
    {
        ThrowIfFailed(
            DeviceEnumerator.EnumDevices(ToDeviceLayer(config.TransportType), out List<IDeviceInfo> devices),
            "Enumerate HikVision cameras failed");

        IDeviceInfo target = FindTargetDevice(devices, config)
            ?? throw new InvalidOperationException(
                $"No matching HikVision camera was found. Serial={config.SerialNumber ?? "<null>"}, IP={config.IpAddress ?? "<null>"}.");

        MvCameraControl.IDevice device = DeviceFactory.CreateDevice(target);
        try
        {
            ThrowIfFailed(device.Open(), "Open HikVision camera failed");
            if (config.ConfigureOptimalPacketSize && device is IGigEDevice gigEDevice)
            {
                ThrowIfFailed(gigEDevice.GetOptimalPacketSize(out int packetSize), "Read HikVision optimal packet size failed");
                if (packetSize > 0)
                {
                    ThrowIfFailed(device.Parameters.SetIntValue("GevSCPSPacketSize", packetSize), "Set HikVision packet size failed");
                }
            }

            hikDevice = device;
        }
        catch
        {
            try
            {
                if (device.IsConnected)
                {
                    device.Close();
                }
            }
            finally
            {
                device.Dispose();
            }

            throw;
        }
    }

    private void ReceiveFrames(CancellationToken cancellationToken)
    {
        uint timeoutMs = checked((uint)Math.Ceiling(config.FrameReceiveTimeout.TotalMilliseconds));
        while (!cancellationToken.IsCancellationRequested)
        {
            MvCameraControl.IDevice? device = hikDevice;
            if (device == null)
            {
                return;
            }

            try
            {
                int result = device.StreamGrabber.GetImageBuffer(timeoutMs, out IFrameOut frameOut);
                if (result != MvError.MV_OK || frameOut == null)
                {
                    continue;
                }

                try
                {
                    if (!HasFrameSubscribers)
                    {
                        continue;
                    }

                    CameraFrame frame = CameraFrame.CreatePooledCopy(
                        frameOut.Image.PixelData,
                        checked((int)frameOut.Image.Width),
                        checked((int)frameOut.Image.Height),
                        frameOut.FrameNum,
                        frameOut.Image.PixelType.ToString(),
                        timestamp: DateTimeOffset.UtcNow);
                    RaiseFrameArrived(frame);
                }
                finally
                {
                    device.StreamGrabber.FreeImageBuffer(frameOut);
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    RaiseErrorOccurred("Receive HikVision frame failed.", ex);
                }
            }
        }
    }

    private void CloseDeviceNoThrow()
    {
        MvCameraControl.IDevice? device = hikDevice;
        hikDevice = null;
        if (device == null)
        {
            return;
        }

        try
        {
            if (device.IsConnected)
            {
                device.Close();
            }
        }
        finally
        {
            device.Dispose();
        }
    }

    private MvCameraControl.IDevice GetDevice()
        => hikDevice is { IsConnected: true } device
            ? device
            : throw new InvalidOperationException("HikVision camera is not connected.");

    private static IDeviceInfo? FindTargetDevice(IReadOnlyList<IDeviceInfo> devices, CameraConfig config)
    {
        bool hasSerial = !string.IsNullOrWhiteSpace(config.SerialNumber);
        bool hasIp = !string.IsNullOrWhiteSpace(config.IpAddress);
        if (!hasSerial && !hasIp)
        {
            return null;
        }

        return devices.FirstOrDefault(device =>
        {
            bool serialMatches = !hasSerial
                || string.Equals(device.SerialNumber, config.SerialNumber, StringComparison.OrdinalIgnoreCase);
            bool ipMatches = !hasIp
                || device is IGigEDeviceInfo gigE
                && string.Equals(FormatIpAddress(gigE.CurrentIp), config.IpAddress, StringComparison.OrdinalIgnoreCase);
            return serialMatches && ipMatches;
        });
    }

    private static DeviceTLayerType ToDeviceLayer(CameraTransportType transportType) => transportType switch
    {
        CameraTransportType.Auto => DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice,
        CameraTransportType.GigE => DeviceTLayerType.MvGigEDevice,
        CameraTransportType.Usb => DeviceTLayerType.MvUsbDevice,
        _ => throw new ArgumentOutOfRangeException(nameof(transportType), transportType, null)
    };

    private static string ToHikTriggerSource(CameraTriggerSource source) => source switch
    {
        CameraTriggerSource.Software => "Software",
        CameraTriggerSource.Line0 => "Line0",
        CameraTriggerSource.Line1 => "Line1",
        CameraTriggerSource.Line2 => "Line2",
        CameraTriggerSource.Line3 => "Line3",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
    };

    private static string FormatIpAddress(uint address)
        => $"{(address & 0xFF000000) >> 24}.{(address & 0x00FF0000) >> 16}.{(address & 0x0000FF00) >> 8}.{address & 0x000000FF}";

    private static void SetEnum(MvCameraControl.IDevice device, string name, string value)
        => ThrowIfFailed(device.Parameters.SetEnumValueByString(name, value), $"Set HikVision parameter {name}={value} failed");

    private static void SetFloat(MvCameraControl.IDevice device, string name, double value)
        => ThrowIfFailed(device.Parameters.SetFloatValue(name, checked((float)value)), $"Set HikVision parameter {name}={value} failed");

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and greater than zero.");
        }
    }

    private static void ThrowIfFailed(int result, string message)
    {
        if (result != MvError.MV_OK)
        {
            throw new InvalidOperationException($"{message}. ErrorCode=0x{result:X8}.");
        }
    }
}
