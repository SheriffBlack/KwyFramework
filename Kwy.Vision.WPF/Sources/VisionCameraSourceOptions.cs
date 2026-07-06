namespace Kwy.Vision.WPF.Sources;

public sealed class VisionCameraSourceOptions
{
    public string? CameraName { get; init; }

    public string? TriggerMode { get; init; }

    public double ExposureMs { get; init; } = 10;

    public double Gain { get; init; } = 1;
}
