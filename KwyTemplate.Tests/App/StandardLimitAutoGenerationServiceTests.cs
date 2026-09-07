using KwyTemplate.App.Models;
using KwyTemplate.App.Services;
using Xunit;

namespace KwyTemplate.Tests.App;

public sealed class StandardLimitAutoGenerationServiceTests
{
    [Fact]
    public void TryApply_DcrAtOrBelow50MilliOhm_UsesAbsoluteMilliOhmDeviation()
    {
        var item = new StandardSampleLimitItemModel("DCR1", "DCR")
        {
            Unit = "mΩ",
            StandardValue = "30"
        };
        var options = new StandardLimitAutoGenerationOptions
        {
            DcrAtOrBelow50MilliOhmNegativeDeviation = 2,
            DcrAtOrBelow50MilliOhmPositiveDeviation = 3
        };

        bool applied = StandardLimitAutoGenerationService.TryApply(item, options);

        Assert.True(applied);
        Assert.Equal("28", item.LowerLimit);
        Assert.Equal("33", item.UpperLimit);
    }

    [Fact]
    public void TryApply_RsAtOrBelowPoint5Ohm_ConvertsMilliOhmDeviationToItemUnit()
    {
        var item = new StandardSampleLimitItemModel("Rs", "Rs")
        {
            Unit = "Ω",
            StandardValue = "0.2"
        };
        var options = new StandardLimitAutoGenerationOptions
        {
            RsAtOrBelow0Point5OhmNegativeDeviation = 10,
            RsAtOrBelow0Point5OhmPositiveDeviation = 20
        };

        bool applied = StandardLimitAutoGenerationService.TryApply(item, options);

        Assert.True(applied);
        Assert.Equal("0.19", item.LowerLimit);
        Assert.Equal("0.22", item.UpperLimit);
    }

    [Fact]
    public void TryApply_Ls2_UsesHighFrequencyRule()
    {
        var item = new StandardSampleLimitItemModel("Ls2", "High Ls")
        {
            Unit = "μH",
            StandardValue = "10"
        };
        var options = new StandardLimitAutoGenerationOptions
        {
            HighLsNegativeDeviationPercent = 5,
            HighLsPositiveDeviationPercent = 10
        };

        bool applied = StandardLimitAutoGenerationService.TryApply(item, options);

        Assert.True(applied);
        Assert.Equal("9.5", item.LowerLimit);
        Assert.Equal("11", item.UpperLimit);
    }

    [Fact]
    public void TryApply_Q_UsesConfiguredAbsoluteLimits()
    {
        var item = new StandardSampleLimitItemModel("Q2", "High Q")
        {
            StandardValue = "100"
        };
        var options = new StandardLimitAutoGenerationOptions
        {
            QLowerLimit = 80,
            QUpperLimit = 120
        };

        bool applied = StandardLimitAutoGenerationService.TryApply(item, options);

        Assert.True(applied);
        Assert.Equal("80", item.LowerLimit);
        Assert.Equal("120", item.UpperLimit);
    }

    [Fact]
    public void TryApply_EmptyCenterValue_ClearsGeneratedLimits()
    {
        var item = new StandardSampleLimitItemModel("Ls", "Ls")
        {
            StandardValue = "",
            LowerLimit = "9.5",
            UpperLimit = "10.5"
        };

        bool applied = StandardLimitAutoGenerationService.TryApply(item, new StandardLimitAutoGenerationOptions());

        Assert.True(applied);
        Assert.Equal(string.Empty, item.LowerLimit);
        Assert.Equal(string.Empty, item.UpperLimit);
    }
}
