using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.WPF.Interaction;
using System.Windows;
using Xunit;

namespace Kwy.Vision.WPF.Tests;

public sealed class VisionRegionEditorTests
{
    private readonly VisionRegionEditor editor = new();

    [Fact]
    public void CreateCircle_UsesStartAsCenterAndClampsRadius()
    {
        var region = Assert.IsType<CircleRegion>(editor.Create(
            VisionRoiCreationKind.Circle,
            new Point(20, 20),
            new Point(80, 20),
            new Size(100, 100)));

        Assert.Equal(new VisionPoint(20, 20), region.Circle.Center);
        Assert.Equal(20, region.Circle.Radius, 10);
    }

    [Fact]
    public void ResizeCircle_ChangesRadiusWithoutMovingCenter()
    {
        var source = new CircleRegion(new VisionCircle(new VisionPoint(50, 50), 10));

        var result = Assert.IsType<CircleRegion>(editor.Resize(
            source, VisionRoiHandle.Radius, new Point(80, 50), new Size(100, 100)));

        Assert.Equal(source.Circle.Center, result.Circle.Center);
        Assert.Equal(30, result.Circle.Radius, 10);
    }

    [Fact]
    public void RotateRectangle_PointsTopTowardRotationHandle()
    {
        var source = new RotatedRectangleRegion(new VisionRotatedRectangle(
            new VisionPoint(50, 50), 40, 20, 0));

        var result = Assert.IsType<RotatedRectangleRegion>(editor.Resize(
            source, VisionRoiHandle.Rotation, new Point(80, 50), new Size(100, 100)));

        Assert.Equal(Math.PI / 2, result.Rectangle.AngleRadians, 10);
    }

    [Fact]
    public void HitTestCircle_DetectsRadiusHandleAndBody()
    {
        var region = new CircleRegion(new VisionCircle(new VisionPoint(30, 30), 10));
        static Point Identity(Point point) => point;

        Assert.Equal(VisionRoiHandle.Radius, editor.HitTest(region, new Point(40, 30), Identity, Identity, 1));
        Assert.Equal(VisionRoiHandle.Body, editor.HitTest(region, new Point(30, 30), Identity, Identity, 1));
        Assert.Equal(VisionRoiHandle.None, editor.HitTest(region, new Point(80, 80), Identity, Identity, 1));
    }

    [Fact]
    public void MoveRotatedRectangle_ClampsItsRotatedBoundsToImage()
    {
        var source = new RotatedRectangleRegion(new VisionRotatedRectangle(
            new VisionPoint(50, 50), 40, 20, Math.PI / 4));

        var result = Assert.IsType<RotatedRectangleRegion>(editor.Move(
            source, new Vector(100, 100), new Size(100, 100)));

        double extent = (40 * Math.Cos(Math.PI / 4) + 20 * Math.Sin(Math.PI / 4)) / 2;
        Assert.Equal(100 - extent, result.Rectangle.Center.X, 8);
        Assert.Equal(100 - extent, result.Rectangle.Center.Y, 8);
    }
}
