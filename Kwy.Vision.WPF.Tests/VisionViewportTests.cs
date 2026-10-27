using Kwy.Vision.WPF.Viewport;
using System.Windows;
using Xunit;

namespace Kwy.Vision.WPF.Tests;

public sealed class VisionViewportTests
{
    [Fact]
    public void Fit_CentersImageAndPreservesAspectRatio()
    {
        var viewport = new VisionViewport();

        viewport.Fit(new Size(100, 50), new Size(400, 300), padding: 0);

        Assert.Equal(4, viewport.Scale, 10);
        AssertPoint(new Point(0, 50), viewport.ImageToView(new Point(0, 0)));
        AssertPoint(new Point(400, 250), viewport.ImageToView(new Point(100, 50)));
    }

    [Fact]
    public void ZoomAt_KeepsImagePointUnderPointer()
    {
        var viewport = new VisionViewport();
        viewport.Fit(new Size(200, 100), new Size(400, 300));
        var pointer = new Point(125, 90);
        Point imageBefore = viewport.ViewToImage(pointer);

        viewport.ZoomAt(pointer, 1.5);

        AssertPoint(pointer, viewport.ImageToView(imageBefore));
    }

    [Fact]
    public void PanAndCoordinateConversion_RoundTrip()
    {
        var viewport = new VisionViewport();
        viewport.Fit(new Size(640, 480), new Size(1280, 720));
        viewport.Pan(new Vector(23, -17));
        var imagePoint = new Point(234.5, 123.25);

        Point roundTrip = viewport.ViewToImage(viewport.ImageToView(imagePoint));

        AssertPoint(imagePoint, roundTrip);
    }

    private static void AssertPoint(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 10);
        Assert.Equal(expected.Y, actual.Y, 10);
    }
}
