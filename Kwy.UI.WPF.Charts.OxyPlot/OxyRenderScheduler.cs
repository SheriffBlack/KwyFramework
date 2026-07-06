using System.Windows.Media;

namespace Kwy.UI.WPF.Charts.OxyPlot;

internal interface IOxyRenderLoop
{
    void OnRenderFrame();
}

internal static class OxyRenderScheduler
{
    private static readonly object SyncRoot = new();
    private static readonly HashSet<IOxyRenderLoop> Charts = [];
    private static IOxyRenderLoop[] snapshot = [];

    public static void Register(IOxyRenderLoop chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        lock (SyncRoot)
        {
            if (!Charts.Add(chart))
            {
                return;
            }

            snapshot = Charts.ToArray();
            if (Charts.Count == 1)
            {
                CompositionTarget.Rendering += OnRendering;
            }
        }
    }

    public static void Unregister(IOxyRenderLoop chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        lock (SyncRoot)
        {
            if (!Charts.Remove(chart))
            {
                return;
            }

            snapshot = Charts.ToArray();
            if (Charts.Count == 0)
            {
                CompositionTarget.Rendering -= OnRendering;
            }
        }
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        var current = snapshot;
        for (int i = 0; i < current.Length; i++)
        {
            current[i].OnRenderFrame();
        }
    }
}
