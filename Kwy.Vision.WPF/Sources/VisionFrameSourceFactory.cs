namespace Kwy.Vision.WPF.Sources;

public sealed class VisionFrameSourceFactory : IVisionFrameSourceFactory
{
    public IVisionFrameSource CreateLocalImageSource(string? source)
        => new LocalImageFrameSource(source);

    public IVisionFrameSource CreateLocalVideoSource(string? source)
        => new LocalVideoFrameSource(source);

    public IVisionFrameSource CreateCameraSource(VisionCameraSourceOptions options)
    {
        string cameraName = options.CameraName?.Trim() ?? string.Empty;
        return new PlaceholderVisionFrameSource(
            string.IsNullOrWhiteSpace(cameraName) ? "相机" : cameraName,
            !string.IsNullOrWhiteSpace(cameraName),
            "相机帧源接口已保留，请接入本机摄像头、GigE/USB3 工业相机或厂商 SDK 的 IVisionFrameSource 实现。");
    }
}
