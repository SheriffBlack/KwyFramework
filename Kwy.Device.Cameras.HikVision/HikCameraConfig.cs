using Kwy.Device.Abstractions.Vision;

namespace Kwy.Device.Cameras.HikVision;

/// <summary>HikVision-specific camera configuration.</summary>
public sealed class HikCameraConfig : CameraConfig
{
    public bool ConfigureOptimalPacketSize { get; set; } = true;

    public string? PixelFormat { get; set; }

    public double? AcquisitionFrameRate { get; set; }

    public override bool Validate()
    {
        return base.Validate()
            && (string.IsNullOrWhiteSpace(PixelFormat) || PixelFormat.Length <= 128)
            && (AcquisitionFrameRate is null
                || double.IsFinite(AcquisitionFrameRate.Value) && AcquisitionFrameRate.Value > 0);
    }
}
