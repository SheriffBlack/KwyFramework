using Kwy.Vision.WPF.Interaction;
using System.Windows;
using Xunit;

namespace Kwy.Vision.WPF.Tests;

public sealed class RectangleRoiEditorTests
{
    private readonly RectangleRoiEditor editor = new();

    [Fact]
    public void Create_NormalizesReverseDragAndClampsToImage()
    {
        Rect result = editor.Create(new Point(80, 70), new Point(-10, 20), new Size(100, 80));

        AssertRect(new Rect(0, 20, 80, 50), result);
    }

    [Fact]
    public void Move_PreservesSizeAndStopsAtImageBoundary()
    {
        Rect result = editor.Move(new Rect(10, 10, 30, 20), new Vector(100, 100), new Size(80, 60));

        AssertRect(new Rect(50, 40, 30, 20), result);
    }

    [Fact]
    public void Resize_AllowsHandleToCrossAndNormalizesResult()
    {
        Rect result = editor.Resize(
            new Rect(20, 20, 30, 30),
            RectangleRoiHandle.TopLeft,
            new Point(70, 60),
            new Size(100, 100));

        AssertRect(new Rect(50, 50, 20, 10), result);
    }

    [Theory]
    [InlineData(10, 10, RectangleRoiHandle.TopLeft)]
    [InlineData(50, 10, RectangleRoiHandle.Top)]
    [InlineData(90, 90, RectangleRoiHandle.BottomRight)]
    [InlineData(50, 50, RectangleRoiHandle.Body)]
    [InlineData(120, 120, RectangleRoiHandle.None)]
    public void HitTest_ReturnsExpectedHandle(double x, double y, RectangleRoiHandle expected)
    {
        RectangleRoiHandle result = editor.HitTest(new Rect(10, 10, 80, 80), new Point(x, y));

        Assert.Equal(expected, result);
    }

    private static void AssertRect(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, 10);
        Assert.Equal(expected.Y, actual.Y, 10);
        Assert.Equal(expected.Width, actual.Width, 10);
        Assert.Equal(expected.Height, actual.Height, 10);
    }
}
