using System.Runtime.CompilerServices;
using System.Windows.Media;
using OxyPlot;

namespace Kwy.UI.WPF.Charts.OxyPlot;

internal static class OxyChartHelpers
{
    public static readonly ConditionalWeakTable<PlotModel, object> WrapperMap = new();

    public static OxyColor ToOxyColor(Color color)
    {
        return OxyColor.FromArgb(color.A, color.R, color.G, color.B);
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
}
