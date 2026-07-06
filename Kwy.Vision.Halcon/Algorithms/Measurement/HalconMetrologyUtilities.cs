using HalconDotNet;
using Kwy.Vision.Abstractions.Algorithms;
using Kwy.Vision.Abstractions.Geometry;

namespace Kwy.Vision.Halcon.Algorithms;

internal static class HalconMetrologyUtilities
{
    public static string ToTransition(VisionMetrologyEdgePolarity polarity) => polarity switch
    {
        VisionMetrologyEdgePolarity.All => "all",
        VisionMetrologyEdgePolarity.Positive => "positive",
        VisionMetrologyEdgePolarity.Negative => "negative",
        _ => throw new ArgumentOutOfRangeException(nameof(polarity), polarity, null)
    };

    public static IReadOnlyList<VisionPoint> GetMeasuredPoints(HMetrologyModel model, int index)
    {
        using HXLDCont measures = model.GetMetrologyObjectMeasures(index, "all", out HTuple rows, out HTuple columns);
        _ = measures;
        var points = new List<VisionPoint>(rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            points.Add(new VisionPoint(columns[i].D, rows[i].D));
        }

        return points;
    }

    public static void SetCommonObjectParameters(
        HMetrologyModel model,
        int index,
        double sigma,
        double threshold,
        VisionMetrologyEdgePolarity polarity,
        int minimumScore)
    {
        model.SetMetrologyObjectParam(index, "measure_sigma", sigma);
        model.SetMetrologyObjectParam(index, "measure_threshold", threshold);
        model.SetMetrologyObjectParam(index, "measure_transition", ToTransition(polarity));
        model.SetMetrologyObjectParam(index, "min_score", minimumScore / 100.0);
    }

    public static void ValidateCommon(
        double measureLength1,
        double measureLength2,
        double sigma,
        double threshold,
        int minimumScore)
    {
        if (!double.IsFinite(measureLength1) || measureLength1 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(measureLength1));
        }

        if (!double.IsFinite(measureLength2) || measureLength2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(measureLength2));
        }

        if (!double.IsFinite(sigma) || sigma <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sigma));
        }

        if (!double.IsFinite(threshold) || threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        if (minimumScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore));
        }
    }
}
