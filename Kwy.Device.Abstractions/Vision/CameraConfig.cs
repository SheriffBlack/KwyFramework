namespace Kwy.Device.Abstractions.Vision;

public enum CameraTransportType
{
    Auto,
    GigE,
    Usb
}

public enum CameraTriggerSource
{
    Software,
    Line0,
    Line1,
    Line2,
    Line3
}

/// <summary>Vendor-independent camera selection and acquisition settings.</summary>
public class CameraConfig : IDeviceConfig
{
    public string DeviceId { get; set; } = "Camera.Main";

    public string DeviceName { get; set; } = "Camera";

    public CameraTransportType TransportType { get; set; } = CameraTransportType.Auto;

    public string? IpAddress { get; set; }

    public string? SerialNumber { get; set; }

    /// <summary>Exposure time in microseconds.</summary>
    public double ExposureTimeUs { get; set; } = 10_000;

    public double Gain { get; set; }

    public bool TriggerModeEnabled { get; set; } = true;

    public CameraTriggerSource TriggerSource { get; set; } = CameraTriggerSource.Software;

    /// <summary>SDK receive timeout used by blocking frame retrieval.</summary>
    public TimeSpan FrameReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Vendor SDK internal frame-buffer node count.</summary>
    public int FrameBufferCount { get; set; } = 4;

    public virtual bool Validate()
    {
        return !string.IsNullOrWhiteSpace(DeviceId)
            && !string.IsNullOrWhiteSpace(DeviceName)
            && (!string.IsNullOrWhiteSpace(IpAddress) || !string.IsNullOrWhiteSpace(SerialNumber))
            && double.IsFinite(ExposureTimeUs) && ExposureTimeUs > 0
            && double.IsFinite(Gain) && Gain >= 0
            && FrameReceiveTimeout > TimeSpan.Zero
            && FrameReceiveTimeout.TotalMilliseconds <= uint.MaxValue
            && FrameBufferCount >= 1;
    }

    public void ValidateAndThrow()
    {
        if (!Validate())
        {
            throw new ArgumentException("Invalid camera configuration.", nameof(CameraConfig));
        }
    }
}
