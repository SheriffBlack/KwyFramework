using Kwy.Vision.WPF.Images;
using System.IO;

namespace Kwy.Vision.WPF.Sources;

public sealed class LocalImageFrameSource : IVisionFrameSource
{
    private readonly string[] files;

    public LocalImageFrameSource(string? source)
    {
        files = VisionMediaFileTypes.ExpandSources(source, VisionMediaKind.Image).ToArray();
        DisplayName = files.Length == 1
            ? Path.GetFileName(files[0])
            : "本地图像";
    }

    public string DisplayName { get; }

    public int? FrameCount => files.Length;

    public bool IsConfigured => files.Length > 0;

    public ValueTask<VisionFrame?> ReadFrameAsync(int index, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (index < 0 || index >= files.Length)
        {
            return ValueTask.FromResult<VisionFrame?>(null);
        }

        string file = files[index];
        var frame = new VisionFrame(
            new LocalFileVisionImage(file),
            Path.GetFileName(file),
            index,
            files.Length);

        return ValueTask.FromResult<VisionFrame?>(frame);
    }

    public async IAsyncEnumerable<VisionFrame> ReadAllFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < files.Length; i++)
        {
            VisionFrame? frame = await ReadFrameAsync(i, cancellationToken).ConfigureAwait(false);
            if (frame != null)
            {
                yield return frame;
            }
        }
    }
}
