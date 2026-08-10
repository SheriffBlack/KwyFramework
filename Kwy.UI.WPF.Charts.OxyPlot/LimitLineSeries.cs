using System.Globalization;
using Kwy.UI.WPF.Charts.Abstractions;
using OxyPlot;
using OxyPlot.Series;

namespace Kwy.UI.WPF.Charts.OxyPlot;

internal sealed class LimitLineSeries : LineSeries
{
    public string LimitText { get; set; } = string.Empty;

    public PlotOrientation Orientation { get; set; }

    public OxyColor LimitTextColor { get; set; } = OxyColors.Automatic;

    public double LabelFontSize { get; set; } = 12;

    public double LabelPadding { get; set; } = 6;

    public override void Render(IRenderContext rc)
    {
        base.Render(rc);

        if (string.IsNullOrWhiteSpace(LimitText) || Points.Count == 0 || XAxis is null || YAxis is null || PlotModel is null)
        {
            return;
        }

        OxyRect plotArea = PlotModel.PlotArea;
        DataPoint anchor = Points[^1];
        ScreenPoint screenPoint = XAxis.Transform(anchor.X, anchor.Y, YAxis);
        OxyColor textColor = LimitTextColor.IsAutomatic() ? Color : LimitTextColor;

        if (Orientation == PlotOrientation.Horizontal)
        {
            double x = Math.Min(screenPoint.X - LabelPadding, plotArea.Right - LabelPadding);
            double y = Math.Max(screenPoint.Y - LabelPadding, plotArea.Top + LabelPadding);
            rc.DrawText(
                new ScreenPoint(x, y),
                LimitText,
                textColor,
                null,
                LabelFontSize,
                400,
                0,
                HorizontalAlignment.Right,
                VerticalAlignment.Bottom);
            return;
        }

        double verticalX = Math.Min(Math.Max(screenPoint.X + LabelPadding, plotArea.Left + LabelPadding), plotArea.Right - LabelPadding);
        double verticalY = plotArea.Top + LabelPadding;
        rc.DrawText(
            new ScreenPoint(verticalX, verticalY),
            LimitText,
            textColor,
            null,
            LabelFontSize,
            400,
            0,
            HorizontalAlignment.Left,
            VerticalAlignment.Top);
    }

    public static string FormatLabel(string label, double value)
    {
        string text = FormatLimitValue(value);
        return string.IsNullOrWhiteSpace(label) ? text : $"{label} {text}";
    }

    private static string FormatLimitValue(double value)
        => value.ToString("0.##########", CultureInfo.InvariantCulture);
}