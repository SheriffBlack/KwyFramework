using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Halcon.Algorithms;

internal static class HalconMeasurementUtilities
{
    public static IReadOnlyList<VisionMeasuredEdge> MeasureEdges(
        HImage image,
        int imageWidth,
        int imageHeight,
        VisionRotatedRectangle region,
        double sigma,
        double threshold,
        VisionEdgePolarity polarity,
        VisionEdgeSelection selection)
    {
        using var measure = new HMeasure(
            region.Center.Y,
            region.Center.X,
            region.AngleRadians,
            region.Width / 2,
            region.Height / 2,
            imageWidth,
            imageHeight,
            "nearest_neighbor");

        measure.MeasurePos(
            image,
            sigma,
            threshold,
            ToHalconPolarity(polarity),
            ToHalconSelection(selection),
            out HTuple rows,
            out HTuple columns,
            out HTuple amplitudes,
            out HTuple distances);

        var edges = new List<VisionMeasuredEdge>(rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            edges.Add(new VisionMeasuredEdge(
                new VisionPoint(columns[i].D, rows[i].D),
                amplitudes[i].D,
                distances.Length > i ? distances[i].D : 0));
        }

        return edges;
    }

    public static string ToHalconPolarity(VisionEdgePolarity polarity) => polarity switch
    {
        VisionEdgePolarity.All => "all",
        VisionEdgePolarity.Positive => "positive",
        VisionEdgePolarity.Negative => "negative",
        _ => throw new ArgumentOutOfRangeException(nameof(polarity), polarity, null)
    };

    public static string ToHalconSelection(VisionEdgeSelection selection) => selection switch
    {
        VisionEdgeSelection.All => "all",
        VisionEdgeSelection.First => "first",
        VisionEdgeSelection.Last => "last",
        _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, null)
    };

    public static void ValidateMeasurement(
        VisionRotatedRectangle region,
        double sigma,
        double threshold)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentException("Measurement region dimensions must be greater than zero.", nameof(region));
        }

        if (!double.IsFinite(sigma) || sigma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma));
        }

        if (!double.IsFinite(threshold) || threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }
    }
}
