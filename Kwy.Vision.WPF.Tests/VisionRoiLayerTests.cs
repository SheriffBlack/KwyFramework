using Kwy.Vision.Abstractions.Geometry;
using Kwy.Vision.WPF.Models;
using Xunit;

namespace Kwy.Vision.WPF.Tests;

public sealed class VisionRoiLayerTests
{
    [Fact]
    public void RegionChange_UpdatesTypeAndSummaryNotifications()
    {
        var layer = new VisionRoiLayer();
        var changed = new List<string?>();
        layer.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        layer.Region = new CircleRegion(new VisionCircle(new VisionPoint(20, 30), 5));

        Assert.Equal("Circle", layer.Type);
        Assert.Contains("R=5", layer.Summary);
        Assert.Contains(nameof(VisionRoiLayer.Region), changed);
        Assert.Contains(nameof(VisionRoiLayer.Type), changed);
        Assert.Contains(nameof(VisionRoiLayer.Summary), changed);
    }

    [Fact]
    public void VisibilityAndLock_AreIndependentLayerStates()
    {
        var layer = new VisionRoiLayer { IsVisible = false, IsLocked = true };

        Assert.False(layer.IsVisible);
        Assert.True(layer.IsLocked);
    }
}
