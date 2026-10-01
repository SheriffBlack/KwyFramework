using Kwy.Communicate.Gem;
using Secs4Net;
using static Secs4Net.Item;

namespace Kwy.Communicate.Tests;

public sealed class GemRoutingTests
{
    [Fact]
    public void S2F41Parser_ParsesCommandAndParameters()
    {
        using var message = new SecsMessage(2, 41, true)
        {
            SecsItem = L(A("START"), L(L(A("PPID"), A("RCP-01"))))
        };

        GemParseResult<GemRemoteCommand> result = new S2F41Parser().Parse(message);

        Assert.True(result.IsSuccess);
        Assert.Equal("START", result.Value?.CommandName);
        Assert.Equal("RCP-01", result.Value?.GetRequiredParameter("PPID").GetString());
    }

    [Fact]
    public void S2F23Parser_ParsesTraceDefinition()
    {
        using var message = new SecsMessage(2, 23, true)
        {
            SecsItem = L(U4(1), A("00000100"), U4(10), U4(1), L(U4(1001)))
        };

        GemParseResult<GemTraceRequest> result = new S2F23Parser().Parse(message);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(1), result.Value?.SampleInterval);
        Assert.Equal((uint)1001, result.Value?.VariableIds.Single().Value);
    }

    [Fact]
    public async Task S1F3Handler_ReturnsValuesInRequestedOrder()
    {
        var registry = new GemRegistry();
        registry.RegisterVariableDefinition(new(new GemVid(1001), "LotId", GemVariableKind.DataVariable));
        registry.RegisterVariableDefinition(new(new GemVid(1002), "State", GemVariableKind.StatusVariable));
        registry.Data.SetVariable(new GemVariable(1001, "LotId", A("LOT-01")));
        registry.Data.SetVariable(new GemVariable(1002, "State", A("IDLE")));
        using var request = new SecsMessage(1, 3, true) { SecsItem = L(U4(1002), U4(1001)) };

        using SecsMessage? reply = await new S1F3Handler(registry).HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal((byte)1, reply.S);
        Assert.Equal((byte)4, reply.F);
        Assert.Equal("IDLE", reply.SecsItem![0].GetString());
        Assert.Equal("LOT-01", reply.SecsItem[1].GetString());
    }

    [Fact]
    public void Router_RejectsDuplicateStreamFunctionRegistration()
    {
        var registry = new GemRegistry();
        var router = new GemPrimaryMessageRouter().Register(new S1F3Handler(registry));

        Assert.Throws<InvalidOperationException>(() => router.Register(new S1F3Handler(registry)));
    }

    [Fact]
    public void EquipmentConstantValidator_RejectsWrongSecsFormat()
    {
        var catalog = new GemInterfaceCatalog();
        catalog.RegisterConstant(new GemEquipmentConstantDefinition(
            new GemEcid(5001),
            "Speed",
            Format: SecsFormat.U4));

        GemValidationResult result = new GemEquipmentConstantValidator(catalog).Validate(
            new[] { new GemEquipmentConstantChange(new GemEcid(5001), A("FAST")) });

        Assert.False(result.IsValid);
        Assert.Contains("expects U4", result.Error);
    }
}
