using Kwy.Vision.WPF.Images;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kwy.Vision.WPF.Sources;

public sealed class LocalVideoFrameSource : IVisionFrameSource
{
    private readonly string[] files;

    public LocalVideoFrameSource(string? source)
    {
        files = VisionMediaFileTypes.ExpandSources(source, VisionMediaKind.Video).ToArray();
    }

    public string DisplayName
        => files.Length switch
        {
            0 => "本地视频",
            1 => Path.GetFileName(files[0]),
            _ => $"本地视频 ({files.Length})"
        };

    public int? FrameCount => files.Length;

    public bool IsConfigured => files.Length > 0;

    public async ValueTask<VisionFrame?> ReadFrameAsync(int index, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (index < 0 || index >= files.Length)
        {
            return null;
        }

        string file = files[index];
        WpfBitmapVisionImage image = await CaptureFrameAsync(file, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
        return new VisionFrame(
            image,
            Path.GetFileName(file),
            index,
            files.Length);
    }

    public async IAsyncEnumerable<VisionFrame> ReadAllFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VisionFrame? frame = await ReadFrameAsync(i, cancellationToken).ConfigureAwait(false);
            if (frame != null)
            {
                yield return frame;
            }
        }
    }

    private static async Task<WpfBitmapVisionImage> CaptureFrameAsync(
        string filePath,
        TimeSpan position,
        CancellationToken cancellationToken)
    {
        Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        return await dispatcher.InvokeAsync(
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var player = new MediaPlayer();
                var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var failed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnOpened(object? sender, EventArgs args) => opened.TrySetResult();
                void OnFailed(object? sender, ExceptionEventArgs args)
                    => failed.TrySetResult(args.ErrorException ?? new InvalidOperationException("无法打开视频文件。"));

                player.MediaOpened += OnOpened;
                player.MediaFailed += OnFailed;

                try
                {
                    player.Open(new Uri(filePath, UriKind.Absolute));
                    Task completed = await Task.WhenAny(opened.Task, failed.Task).ConfigureAwait(true);
                    if (completed == failed.Task)
                    {
                        throw await failed.Task.ConfigureAwait(true);
                    }

                    if (position > TimeSpan.Zero)
                    {
                        player.Position = position;
                        await Task.Delay(80, cancellationToken).ConfigureAwait(true);
                    }
                    else
                    {
                        await Task.Delay(40, cancellationToken).ConfigureAwait(true);
                    }

                    int width = Math.Max(1, player.NaturalVideoWidth);
                    int height = Math.Max(1, player.NaturalVideoHeight);
                    if (width == 1 || height == 1)
                    {
                        throw new InvalidOperationException("视频已打开，但无法读取有效画面尺寸。");
                    }

                    var visual = new DrawingVisual();
                    using (DrawingContext dc = visual.RenderOpen())
                    {
                        dc.DrawVideo(player, new Rect(0, 0, width, height));
                    }

                    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    bitmap.Freeze();
                    return new WpfBitmapVisionImage(bitmap, "LocalVideo", File.GetLastWriteTimeUtc(filePath));
                }
                finally
                {
                    player.MediaOpened -= OnOpened;
                    player.MediaFailed -= OnFailed;
                    player.Close();
                }
            },
            DispatcherPriority.Background,
            cancellationToken).Task.Unwrap().ConfigureAwait(false);
    }
}
