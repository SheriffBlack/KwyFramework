using Kwy.Communicate.Core;
using Kwy.Communicate.Visa;

namespace Kwy.Communicate.Tests;

public sealed class VisaCommunicationTests
{
    [Fact]
    public void GpibConfig_BuildsStandardVisaResourceName()
    {
        var config = new GpibConfig
        {
            BoardNumber = 0,
            PrimaryAddress = 23,
            SecondaryAddress = 0
        };

        Assert.Equal("GPIB0::23::INSTR", config.ResourceName);
        Assert.True(config.Validate());
    }

    [Fact]
    public void GpibConfig_IncludesSecondaryAddressWhenConfigured()
    {
        var config = new GpibConfig
        {
            BoardNumber = 1,
            PrimaryAddress = 8,
            SecondaryAddress = 2
        };

        Assert.Equal("GPIB1::8::2::INSTR", config.ResourceName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void GpibConfig_RejectsInvalidPrimaryAddress(int address)
    {
        var config = new GpibConfig();

        Assert.Throws<ArgumentOutOfRangeException>(() => config.PrimaryAddress = address);
    }

    [Fact]
    public void RegisterVisa_CreatesClientsForBothConfigurationStyles()
    {
        CommunicationFactory factory = new CommunicationFactoryBuilder()
            .RegisterVisa()
            .Build();

        using var gpibClient = factory.CreateClient(new GpibConfig { PrimaryAddress = 23 });
        using var visaClient = factory.CreateClient(new VisaConfig { ResourceName = "GPIB0::23::INSTR" });

        Assert.IsType<VisaCommunication>(gpibClient);
        Assert.IsType<VisaCommunication>(visaClient);
    }
}
