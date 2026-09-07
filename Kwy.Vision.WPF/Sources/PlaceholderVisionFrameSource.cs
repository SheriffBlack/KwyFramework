namespace Kwy.Vision.WPF.Sources;

public sealed class PlaceholderVisionFrameSource : IVisionFrameSource
{
    private readonly string message;

    public PlaceholderVisionFrameSource(string displayName, bool isConfigured, string message)
    {
        DisplayName = displayName;
        IsConfigured = isConfigured;
        this.message = message;
    }

    public string DisplayName { get; }

    public int? FrameCount => null;

    public bool IsConfigured { get; }

    public ValueTask<VisionFrame?> ReadFrameAsync(int index, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(message);
    }

    public async IAsyncEnumerable<VisionFrame> ReadAllFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
