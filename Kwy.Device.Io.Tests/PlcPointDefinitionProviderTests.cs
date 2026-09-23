using Kwy.Device.Abstractions.PLC;
using Kwy.Device.Core.PLC;
using Xunit;

namespace Kwy.Device.Io.Tests;

public sealed class PlcPointDefinitionProviderTests
{
    [Fact]
    public void Provider_ResolvesPointByStableIdIgnoringCase()
    {
        var point = Point("Clamp.Closed", "M100");
        var provider = new PlcPointDefinitionProvider([point]);

        Assert.Same(point, provider.GetRequired("clamp.closed"));
    }

    [Fact]
    public void Provider_RejectsDuplicateStableIds()
    {
        Assert.Throws<InvalidOperationException>(() => new PlcPointDefinitionProvider([
            Point("Clamp.Closed", "M100"),
            Point("clamp.closed", "M101")
        ]));
    }

    [Fact]
    public void Provider_RejectsDuplicatePhysicalAddresses()
    {
        Assert.Throws<InvalidOperationException>(() => new PlcPointDefinitionProvider([
            Point("Clamp.Closed", "M100"),
            Point("Clamp.Open", "m100")
        ]));
    }

    private static PlcPointDefinition Point(string id, string address) => new()
    {
        Id = id,
        Name = id,
        DeviceId = "PLC.Main",
        Address = address,
        DataType = PlcDataType.Boolean
    };
}
