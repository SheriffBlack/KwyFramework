using System.Windows;
using System.Windows.Media;

namespace Kwy.Vision.WPF.Viewport;

/// <summary>Maintains the image-to-view matrix used by an interactive vision canvas.</summary>
public sealed class VisionViewport
{
    public const double DefaultMinimumScale = 0.02;
    public const double DefaultMaximumScale = 80;

    private Matrix matrix = Matrix.Identity;

    public double MinimumScale { get; set; } = DefaultMinimumScale;

    public double MaximumScale { get; set; } = DefaultMaximumScale;

    public Matrix Matrix => matrix;

    public double Scale => matrix.M11;

    public void Fit(Size imageSize, Size viewSize, double padding = 0)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || viewSize.Width <= 0 || viewSize.Height <= 0)
        {
            matrix = Matrix.Identity;
            return;
        }

        double availableWidth = Math.Max(1, viewSize.Width - padding * 2);
        double availableHeight = Math.Max(1, viewSize.Height - padding * 2);
        double scale = ClampScale(Math.Min(availableWidth / imageSize.Width, availableHeight / imageSize.Height));
        double x = (viewSize.Width - imageSize.Width * scale) / 2;
        double y = (viewSize.Height - imageSize.Height * scale) / 2;
        matrix = new Matrix(scale, 0, 0, scale, x, y);
    }

    public void Pan(Vector delta)
    {
        matrix.Translate(delta.X, delta.Y);
    }

    public void ZoomAt(Point viewPoint, double factor)
    {
        double targetScale = ClampScale(Scale * factor);
        if (Scale <= 0 || Math.Abs(targetScale - Scale) < double.Epsilon)
        {
            return;
        }

        Point imagePoint = ViewToImage(viewPoint);
        matrix = new Matrix(targetScale, 0, 0, targetScale,
            viewPoint.X - imagePoint.X * targetScale,
            viewPoint.Y - imagePoint.Y * targetScale);
    }

    public Point ImageToView(Point point) => matrix.Transform(point);

    public Point ViewToImage(Point point)
    {
        if (!matrix.HasInverse)
        {
            return point;
        }

        Matrix inverse = matrix;
        inverse.Invert();
        return inverse.Transform(point);
    }

    private double ClampScale(double value) => Math.Clamp(value, MinimumScale, MaximumScale);
}
