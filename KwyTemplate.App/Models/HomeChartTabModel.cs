using Kwy.MVVM.Core;

using Kwy.UI.Charts.Abstractions.Values;

namespace KwyTemplate.App.Models;

public sealed class HomeChartTabModel : BindableBase
{
    private const int MaxSampleCount = 2000;

    private readonly List<ChartValuePoint> samples = [];
    private ChartValuePoint? latestSample;
    private ChartLimitRange? limits;

    public string ParameterId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<ChartValuePoint> Samples => samples;

    public ChartValuePoint? LatestSample
    {
        get => latestSample;
        private set => SetProperty(ref latestSample, value);
    }

    public ChartLimitRange? Limits
    {
        get => limits;
        set => SetProperty(ref limits, value);
    }

    public void AddSample(ChartValuePoint sample)
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
