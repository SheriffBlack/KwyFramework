using KwyTemplate.Vision.Rendering;
using System.Windows;
using System.Windows.Media;

namespace KwyTemplate.Vision.Controls;

public sealed class VisionLiveImageViewer : FrameworkElement, IDisposable
{
    private readonly VisionLiveImageRenderer renderer;

    public VisionLiveImageViewer()
    {
        ClipToBounds = true;
        renderer = new VisionLiveImageRenderer(Dispatcher);
        renderer.ImageSourceChanged += OnImageSourceChanged;
    }

    public long RenderedFrameCount => renderer.RenderedFrameCount;

    public long DroppedFrameCount => renderer.DroppedFrameCount;

    public bool TryRender(VisionLiveFrame frame)
    {
        bool accepted = renderer.TryRender(frame);
        if (accepted)
        {
            InvalidateVisual();
        }

        return accepted;
    }

    public void Dispose()
    {
        renderer.ImageSourceChanged -= OnImageSourceChanged;
        renderer.Dispose();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(new SolidColorBrush(Color.FromRgb(22, 26, 31)), null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (renderer.ImageSource == null)
        {
            return;
        }

        drawingContext.DrawImage(renderer.ImageSource, GetImageRect(renderer.ImageSource));
    }

    private Rect GetImageRect(ImageSource source)
    {
        double width = source.Width;
        double height = source.Height;
        if (width <= 0 || height <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return new Rect(0, 0, ActualWidth, ActualHeight);
        }

        double scale = Math.Min(ActualWidth / width, ActualHeight / height);
        double viewWidth = width * scale;
        double viewHeight = height * scale;
        return new Rect((ActualWidth - viewWidth) / 2, (ActualHeight - viewHeight) / 2, viewWidth, viewHeight);
    }

    private void OnImageSourceChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}
