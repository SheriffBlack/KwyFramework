using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.WPF.Rendering;
using System.Windows;
using Xunit;

namespace Kwy.Vision.WPF.Tests;

public sealed class VisionRegionGeometryFactoryTests
{
    [Fact]
    public void CircleRegion_ProducesExpectedBounds()
    {
        var region = new CircleRegion(new VisionCircle(new VisionPoint(30, 40), 10));

        Rect bounds = VisionRegionGeometryFactory.Create(region).Bounds;

        AssertRect(new Rect(20, 30, 20, 20), bounds);
    }

    [Fact]
    public void RotatedRectangle_ProducesRotatedBounds()
    {
        var region = new RotatedRectangleRegion(new VisionRotatedRectangle(
            new VisionPoint(50, 50), 40, 20, Math.PI / 2));

        Rect bounds = VisionRegionGeometryFactory.Create(region).Bounds;

        AssertRect(new Rect(40, 30, 20, 40), bounds);
    }

    [Fact]
    public void CompositeRegion_UsesEvenOddFillForHole()
    {
        var region = new CompositeRegion(
            new RectangleRegion(new VisionRectangle(0, 0, 100, 100)),
            [new CircleRegion(new VisionCircle(new VisionPoint(50, 50), 10))]);

        var geometry = VisionRegionGeometryFactory.Create(region);

        Assert.False(geometry.FillContains(new Point(50, 50)));
        Assert.True(geometry.FillContains(new Point(10, 10)));
    }

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 8);
        Assert.Equal(expected.Y, actual.Y, 8);
        Assert.Equal(expected.Width, actual.Width, 8);
        Assert.Equal(expected.Height, actual.Height, 8);
    }
}
