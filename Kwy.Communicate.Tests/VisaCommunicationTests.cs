using Ivi.Visa;
using Kwy.Communicate.Core;
using Kwy.Communicate.Visa;

namespace Kwy.Communicate.Tests;

public sealed class VisaCommunicationTests
{
    [Fact]
    public void RuntimeAvailabilityCheck_DoesNotThrowWhenNoResourcesAreInstalled()
    {
        VisaRuntimeStatus status = VisaRuntime.CheckAvailability();

        Assert.NotNull(status);
        Assert.False(string.IsNullOrWhiteSpace(status.Message));
    }

    [Fact]
    public void RuntimeUnavailableClassifier_RecognizesVisaLibraryNotFound()
    {
        var exception = new NativeVisaException(NativeErrorCode.LibraryNotFound);

        Assert.True(VisaRuntime.IsRuntimeUnavailable(exception));
    }

    [Fact]
    public void ResourceNotFound_IsNotClassifiedAsRuntimeUnavailable()
    {
        var exception = new NativeVisaException(NativeErrorCode.ResourceNotFound);

        Assert.True(VisaRuntime.IsResourceNotFound(exception));
        Assert.False(VisaRuntime.IsRuntimeUnavailable(exception));
    }

    [Fact]
    public void RuntimeUnavailableException_PreservesResourceAndCause()
    {
        var cause = new NativeVisaException(NativeErrorCode.LibraryNotFound);

        VisaRuntimeUnavailableException exception = VisaRuntime.CreateUnavailableException(
            "GPIB0::23::INSTR",
            cause);

        Assert.Equal("GPIB0::23::INSTR", exception.ResourceName);
        Assert.Same(cause, exception.InnerException);
        Assert.Contains("GPIB 本机驱动", exception.Message, StringComparison.Ordinal);
        Assert.Contains("NI-488.2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InterfaceUnavailableException_ExplainsThatConfiguredResourceMayNotExist()
    {
        var cause = new NativeVisaException(NativeErrorCode.LibraryNotFound);

        VisaInterfaceUnavailableException exception =
            VisaRuntime.CreateInterfaceUnavailableException("GPIB0::1::INSTR", cause);

        Assert.Equal("GPIB0::1::INSTR", exception.ResourceName);
        Assert.Contains("VISA Runtime", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GPIB", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceNotFoundException_DoesNotClaimThatRuntimeIsMissing()
    {
        VisaResourceNotFoundException exception =
            VisaRuntime.CreateResourceNotFoundException("GPIB0::1::INSTR");

        Assert.Equal("GPIB0::1::INSTR", exception.ResourceName);
        Assert.Contains("未发现 VISA 资源", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("未检测到可用的 VISA Runtime", exception.Message, StringComparison.Ordinal);
    }

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
