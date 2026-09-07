namespace Kwy.Vision.WPF.Sources;

public interface IVisionFrameSourceFactory
{
    IVisionFrameSource CreateLocalImageSource(string? source);

    IVisionFrameSource CreateLocalVideoSource(string? source);

    IVisionFrameSource CreateCameraSource(VisionCameraSourceOptions options);
}
