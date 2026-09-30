using Kwy.Communicate.Core;
using Kwy.Communicate.OpcUa;
using Opc.Ua;

namespace Kwy.Communicate.Tests;

public sealed class OpcUaConfigurationTests
{
    [Fact]
    public void Validate_AcceptsMatchingNoSecurityConfiguration()
    {
        var config = CreateValidConfig();

        Assert.True(config.Validate());
    }

    [Fact]
    public void Validate_RejectsMismatchedSecurityModeAndPolicy()
    {
        var config = CreateValidConfig();
        config.SecurityMode = MessageSecurityMode.SignAndEncrypt;

        Assert.False(config.Validate());
    }

    [Fact]
    public void Defaults_DoNotAutomaticallyTrustUntrustedCertificates()
    {
        Assert.False(new OpcUaConfig().AutoAcceptUntrustedCertificates);
    }

    [Fact]
    public void RegisterOpcUa_CreatesClientWithoutDependencyInjection()
    {
        CommunicationFactory factory = new CommunicationFactoryBuilder().RegisterOpcUa().Build();

        using var client = factory.CreateClient(CreateValidConfig());

        Assert.IsType<OpcUaCommunication>(client);
    }

    private static OpcUaConfig CreateValidConfig()
        => new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            SecurityMode = MessageSecurityMode.None,
            SecurityPolicy = SecurityPolicies.None
        };
}
