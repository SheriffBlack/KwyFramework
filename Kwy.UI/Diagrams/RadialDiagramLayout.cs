namespace Kwy.UI.Diagrams;

/// <summary>
/// 将节点按圆周均匀排布的无 UI 布局算法。
/// </summary>
public static class RadialDiagramLayout
{
    public static IReadOnlyList<DiagramPoint> Arrange(
        DiagramPoint center,
        double radius,
        int count,
        double startAngle = -90d)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(startAngle))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }

        var points = new DiagramPoint[count];
        double step = 360d / count;

        for (int index = 0; index < count; index++)
        {
            double radians = (startAngle + (step * index)) * Math.PI / 180d;
            points[index] = new DiagramPoint(
                center.X + (radius * Math.Cos(radians)),
                center.Y + (radius * Math.Sin(radians)));
        }

        return points;
    }
}
