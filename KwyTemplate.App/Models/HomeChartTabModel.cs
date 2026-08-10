using Kwy.MVVM.Core;

namespace KwyTemplate.App.Models;

public sealed class HomeChartTabModel : BindableBase
{
    private const int MaxSampleCount = 2000;

    private readonly List<ChartValueSample> samples = [];
    private ChartValueSample? latestSample;
    private ChartLimitSet? limits;

    public string ParameterId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<ChartValueSample> Samples => samples;

    public ChartValueSample? LatestSample
    {
        get => latestSample;
        private set => SetProperty(ref latestSample, value);
    }

    public ChartLimitSet? Limits
    {
        get => limits;
        set => SetProperty(ref limits, value);
    }

    public void AddSample(ChartValueSample sample)
    {
        samples.Add(sample);
        if (samples.Count > MaxSampleCount)
        {
            samples.RemoveRange(0, samples.Count - MaxSampleCount);
        }

        LatestSample = sample;
    }

    public void ClearSamples()
    {
        samples.Clear();
        LatestSample = null;
    }
}

public sealed class ChartValueSample
{
    public ChartValueSample(long sequence, double value, bool? isPass = null)
    {
        Sequence = sequence;
        Value = value;
        IsPass = isPass;
    }

    public long Sequence { get; }

    public double Value { get; }

    public bool? IsPass { get; }
}

public sealed class ChartLimitSet
{
    public ChartLimitSet(double? lowerLimit, double? upperLimit, double? targetValue = null)
    {
        LowerLimit = lowerLimit;
        UpperLimit = upperLimit;
        TargetValue = targetValue;
    }

    public double? LowerLimit { get; }

    public double? UpperLimit { get; }

    public double? TargetValue { get; }
}
