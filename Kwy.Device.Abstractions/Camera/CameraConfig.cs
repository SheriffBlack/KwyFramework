namespace Kwy.Device.Abstractions.Camera;

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

/// <summary>厂商无关的相机选择与采集配置。</summary>
public class CameraConfig : IDeviceConfig
{
    public string DeviceId { get; set; } = "Camera.Main";

    public string DeviceName { get; set; } = "Camera";

    public CameraTransportType TransportType { get; set; } = CameraTransportType.Auto;

    public string? IpAddress { get; set; }

    public string? SerialNumber { get; set; }

    /// <summary>曝光时间，单位为微秒。</summary>
    public double ExposureTimeUs { get; set; } = 10_000;

    public double Gain { get; set; }

    public bool TriggerModeEnabled { get; set; } = true;

    public CameraTriggerSource TriggerSource { get; set; } = CameraTriggerSource.Software;

    /// <summary>同步取帧时传给厂商 SDK 的接收超时。</summary>
    public TimeSpan FrameReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>厂商 SDK 内部帧缓存节点数。</summary>
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
