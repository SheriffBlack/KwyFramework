using Kwy.Converter;
using Xunit;

namespace KwyTemplate.Tests.Flow;

public sealed class MeasurementUnitConverterTests
{
    [Theory]
    [InlineData("L_S", 0.000995d, "μH", 0.995d)]
    [InlineData("R_S", 1.9695d, "mΩ", 1969.5d)]
    [InlineData("C_S", 0.000000047d, "nF", 47d)]
    public void FromBaseUnit_HiokiProtocolParameter_UsesEngineeringUnit(
        string parameter,
        double baseValue,
        string targetUnit,
        double expected)
    {
        double actual = MeasurementUnitConverter.FromBaseUnit(baseValue, parameter, targetUnit);

        Assert.Equal(expected, actual, 8);
    }

    [Fact]
    public void Convert_ResistiveUpperLimit_NormalizesToLowerLimitUnit()
    {
        double actual = MeasurementUnitConverter.Convert(2.5d, "R_S", "Ω", "mΩ");

        Assert.Equal(2500d, actual, 8);
    }
}
