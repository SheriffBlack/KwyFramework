namespace Kwy.Vision.WPF.Sources;

public interface IVisionFrameSource
{
    string DisplayName { get; }

    int? FrameCount { get; }

    bool IsConfigured { get; }

    ValueTask<VisionFrame?> ReadFrameAsync(int index, CancellationToken cancellationToken = default);

    IAsyncEnumerable<VisionFrame> ReadAllFramesAsync(CancellationToken cancellationToken = default);
}
