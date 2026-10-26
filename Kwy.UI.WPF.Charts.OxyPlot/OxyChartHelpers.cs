using System.Runtime.CompilerServices;
using Kwy.UI.Primitives;
using OxyPlot;
using OxyPlot.Axes;

namespace Kwy.UI.WPF.Charts.OxyPlot;

internal static class OxyChartHelpers
{
    public static readonly ConditionalWeakTable<PlotModel, object> WrapperMap = new();

    public static readonly OxyColor KwyLimitColor = OxyColor.FromRgb(0xF4, 0x43, 0x36);

    public static readonly OxyColor KwyTargetColor = OxyColor.FromRgb(0x00, 0x80, 0x00);

    public static OxyColor ToOxyColor(Rgba32 color)
    {
        return OxyColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }

    public static PlotController CreateTrackOnlyController()
    {
        var controller = new PlotController();
        controller.UnbindAll();
        controller.BindMouseEnter(PlotCommands.HoverTrack);
        return controller;
    }

    public static void RegisterWrapper(PlotModel model, object wrapper)
    {
        WrapperMap.Remove(model);
        WrapperMap.Add(model, wrapper);
    }
    public static string FormatAdaptiveAxisLabel(double value, Axis? axis)
    {
        if (axis is null)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        }

        double step = axis.ActualMajorStep;
        if (!double.IsFinite(step) || step <= 0)
        {
            step = Math.Abs(axis.ActualMaximum - axis.ActualMinimum) / 5.0;
        }

        if (!double.IsFinite(step) || step <= 0)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
        }

        int decimals = step >= 1
            ? 0
            : Math.Min(8, Math.Max(2, (int)Math.Ceiling(-Math.Log10(step)) + 1));
        return value.ToString("0." + new string('#', decimals), System.Globalization.CultureInfo.CurrentCulture);
    }
}


