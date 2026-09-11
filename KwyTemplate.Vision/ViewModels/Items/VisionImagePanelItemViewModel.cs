using Kwy.Vision.Abstractions.Images;
using Kwy.Vision.Abstractions.Results;
using Kwy.Vision.WPF.Images;
using KwyTemplate.Vision.Executors;
using System.Windows.Media;

namespace KwyTemplate.Vision.ViewModels.Items;

public sealed class VisionImagePanelItemViewModel
{
    private ImageSource? thumbnailSource;
    private bool thumbnailCreated;
    private byte[]? pixels;
    private Task<byte[]>? pixelLoadTask;

    public string NodeId { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public string PortName { get; init; } = string.Empty;

    public string Direction { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public int? SequenceIndex { get; init; }

    public int? SequenceCount { get; init; }

    public string PositionText
        => SequenceIndex is int index && SequenceCount is int count && count > 1
            ? $"{index + 1}/{count}"
            : string.Empty;

    public int OverlayCount { get; init; }

    public IVisionImage Image { get; init; } = null!;

    public byte[]? Pixels => pixels;

    public bool HasPixels => pixels is { Length: > 0 };

    public int Width { get; init; }

    public int Height { get; init; }

    public int Stride { get; init; }

    public VisionPixelFormat PixelFormat { get; init; }

    public IReadOnlyList<IVisionOverlayShape> Overlays { get; init; } = Array.Empty<IVisionOverlayShape>();

    public ImageSource? ThumbnailSource
    {
        get
        {
            if (!thumbnailCreated)
            {
                thumbnailSource = VisionImageThumbnailFactory.CreateFromImage(Image, pixels, 96, 64);
                thumbnailCreated = true;
            }

            return thumbnailSource;
        }
    }

    public static Task<VisionImagePanelItemViewModel> CreateAsync(
        FlowImageSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new VisionImagePanelItemViewModel
        {
            NodeId = snapshot.NodeId,
            NodeName = snapshot.NodeName,
            PortName = snapshot.PortName,
            Direction = snapshot.Direction.ToString(),
            Summary = snapshot.Summary,
            SequenceIndex = snapshot.SequenceIndex,
            SequenceCount = snapshot.SequenceCount,
            OverlayCount = snapshot.Overlays.Count,
            Image = snapshot.Image,
            Width = snapshot.Image.Width,
            Height = snapshot.Image.Height,
            Stride = snapshot.Image.Stride,
            PixelFormat = snapshot.Image.PixelFormat,
            Overlays = snapshot.Overlays
        });
    }

    public Task<byte[]> EnsurePixelsAsync(CancellationToken cancellationToken = default)
    {
        if (pixels != null)
        {
            return Task.FromResult(pixels);
        }

        return pixelLoadTask ??= LoadPixelsAsync(cancellationToken);
    }

    private async Task<byte[]> LoadPixelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ReadOnlyMemory<byte> memory = await Image.GetPixelMemoryAsync(cancellationToken).ConfigureAwait(false);
            pixels = memory.ToArray();
            return pixels;
        }
        catch
        {
            pixels = Array.Empty<byte>();
            return pixels;
        }
    }

}
