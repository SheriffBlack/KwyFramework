using Kwy.UI.WPF.FlowDesigner.Controls;
using System.Globalization;
using System.Windows.Data;

namespace KwyTemplate.Vision.Converters;

public sealed class ConnectionProbePositionConverter : IMultiValueConverter
{
    public string Axis { get; set; } = "X";

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 7
            || values[0] is not double sourceX
            || values[1] is not double sourceY
            || values[2] is not double targetX
            || values[3] is not double targetY)
        {
            return 0.0;
        }

        string targetSide = values[5]?.ToString() ?? "Left";
        var style = values[6] is ConnectionStyle connectionStyle ? connectionStyle : ConnectionStyle.Bezier;
        bool isOrthogonal = style is ConnectionStyle.Orthogonal or ConnectionStyle.Circuit;

        var label = isOrthogonal
            ? GetOrthogonalLabelPoint(sourceX, sourceY, targetX, targetY, targetSide)
            : ((sourceX + targetX) / 2.0, (sourceY + targetY) / 2.0);

        return string.Equals(Axis, "Y", StringComparison.OrdinalIgnoreCase)
            ? label.Item2
            : label.Item1;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static (double X, double Y) GetOrthogonalLabelPoint(
        double sourceX,
        double sourceY,
        double targetX,
        double targetY,
        string targetSide)
    {
        const double offset = 8.0;
        const double labelHalfHeight = 12.0;

        return targetSide switch
        {
            "Top" or "Bottom" => (targetX + offset, ((sourceY + targetY) / 2.0) - labelHalfHeight),
            "Right" => (((sourceX + targetX) / 2.0), targetY + offset),
            _ => (((sourceX + targetX) / 2.0), targetY - labelHalfHeight)
        };
    }
}
