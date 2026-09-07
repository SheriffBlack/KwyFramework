using System.Text.Json;
using Kwy.Device.Instruments.Dcr;
using Kwy.Device.Instruments.Lcr;
using Xunit;

namespace KwyTemplate.Tests.Instruments;

public sealed class InstrumentLimitConfigurationTests
{
    [Fact]
    public void AdexDcrConfig_UsesEngineeringLimits_AndSerializesNewNames()
    {
        var config = new AdexDcrConfig
        {
            LowerLimit = 79,
            LowerLimitUnit = "mΩ",
            UpperLimit = 98,
            UpperLimitUnit = "mΩ"
        };

        Assert.True(config.Validate());

        string json = JsonSerializer.Serialize(config);
        Assert.Contains("\"LowerLimit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"UpperLimit\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("LowerLimitRaw", json, StringComparison.Ordinal);
        Assert.DoesNotContain("UpperLimitRaw", json, StringComparison.Ordinal);

        AdexDcrConfig restored = JsonSerializer.Deserialize<AdexDcrConfig>(json)!;
        Assert.Equal(79, restored.LowerLimit);
        Assert.Equal("mΩ", restored.LowerLimitUnit);
        Assert.Equal(98, restored.UpperLimit);
        Assert.Equal("mΩ", restored.UpperLimitUnit);
    }

    [Fact]
    public void HiokiLcrConfig_SingleFrequency_UsesRenamedLimitsForActiveSettings()
    {
        var config = new HiokiLcrConfig
        {
            FrequencyMode = HiokiLcrConfig.SingleFrequency,
            LoadType = "Ls-Rs",
            Frequency = 5,
            FrequencyUnit = "MHz",
            Parameter1LowerLimit = 0.83,
            Parameter1LowerLimitUnit = "μH",
            Parameter1UpperLimit = 1.18,
            Parameter1UpperLimitUnit = "μH",
            Parameter3LowerLimit = 930,
            Parameter3LowerLimitUnit = "mΩ",
            Parameter3UpperLimit = 2500,
            Parameter3UpperLimitUnit = "mΩ"
        };

        HiokiLcrMeasurementSettings active = config.GetActiveMeasurementSettings();

        Assert.Equal("L_S", active.Parameters.Parameter1);
        Assert.Equal("R_S", active.Parameters.Parameter3);
        Assert.Equal(0.83, active.PrimaryLimit.Minimum);
        Assert.Equal(1.18, active.PrimaryLimit.Maximum);
        Assert.Equal("μH", active.PrimaryLimit.MinimumUnit);
        Assert.Equal(930, active.SecondaryLimit.Minimum);
        Assert.Equal(2500, active.SecondaryLimit.Maximum);
        Assert.Equal("mΩ", active.SecondaryLimit.MaximumUnit);
    }

    [Fact]
    public void HiokiLcrConfig_DualFrequency_UsesRenamedSecondFrequencyLimits()
    {
        var config = new HiokiLcrConfig
        {
            SupportsDualFrequency = true,
            FrequencyMode = HiokiLcrConfig.DualFrequency,
            SecondLoadType = "Ls-Q",
            Frequency2 = 1,
            Frequency2Unit = "MHz",
            SecondParameter1LowerLimit = 0.5,
            SecondParameter1LowerLimitUnit = "μH",
            SecondParameter1UpperLimit = 1.26,
            SecondParameter1UpperLimitUnit = "μH",
            SecondParameter3LowerLimit = 10,
            SecondParameter3LowerLimitUnit = string.Empty,
            SecondParameter3UpperLimit = 200,
            SecondParameter3UpperLimitUnit = string.Empty
        };

        HiokiLcrMeasurementSettings active = config.GetActiveMeasurementSettings();

        Assert.Equal("L_S", active.Parameters.Parameter1);
        Assert.Equal("Q", active.Parameters.Parameter3);
        Assert.Equal(0.5, active.PrimaryLimit.Minimum);
        Assert.Equal(1.26, active.PrimaryLimit.Maximum);
        Assert.Equal(10, active.SecondaryLimit.Minimum);
        Assert.Equal(200, active.SecondaryLimit.Maximum);
    }

    [Fact]
    public void HiokiLcrConfig_OffOff_RemainsDisabledAfterNormalization()
    {
        var config = new HiokiLcrConfig
        {
            LoadType = HiokiLcrLoadTypes.OffOff
        };

        Assert.Equal(HiokiLcrLoadTypes.OffOff, config.LoadType);
        Assert.True(config.IsMeasurementDisabled);

        HiokiLcrParameterPair pair = config.GetActiveParameterPair();
        Assert.Equal("OFF", pair.Parameter1);
        Assert.Equal("OFF", pair.Parameter3);
    }
}
