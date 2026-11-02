using Kwy.Communicate.Gem;
using Secs4Net;
using System.Text;

namespace Kwy.Communicate.Tests;

public sealed class GemRegistryTests
{
    [Fact]
    public async Task JsonProfile_LoadsCatalogMapsHandlerAndCreatesReleaseManifest()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "profileId": "fab-a-aoi-01",
              "interfaceVersion": "1.2.0",
              "variables": [
                { "id": 1001, "name": "LotId", "kind": "DataVariable", "format": "ASCII" }
              ],
              "reports": [ { "id": 2001, "variableIds": [1001] } ],
              "events": [ { "id": 3001, "name": "LotStarted", "reportIds": [2001] } ],
              "remoteCommands": [
                {
                  "name": "START",
                  "handlerKey": "machine.start",
                  "parameters": [ { "name": "PPID", "format": "ASCII" } ],
                  "allowedControlStates": [ "OnlineRemote" ],
                  "acceptedEventId": 3001
                }
              ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        GemJsonInterfaceProfile profile = await GemInterfaceProfileLoader.LoadJsonAsync(stream);
        var registry = new GemRegistry();

        registry.ApplyProfile(profile);
        registry.RegisterCommand("machine.start", (_, _) => Task.FromResult(
            new GemRemoteCommandResult(GemHostCommandAckCode.Accepted)));
        registry.SealConfiguration();
        GemInterfaceReleaseManifest manifest = registry.CreateReleaseManifest("2.3.4");

        Assert.Equal("fab-a-aoi-01", registry.AppliedProfile?.ProfileId);
        Assert.Equal(64, registry.AppliedProfile?.ContentSha256?.Length);
        Assert.Equal("machine.start", registry.Catalog.RemoteCommands["START"].EffectiveHandlerKey);
        Assert.Equal("2.3.4", manifest.SoftwareVersion);
        Assert.Contains("Profile SHA-256", GemInterfaceCatalogExporter.ToMarkdown(registry, "AOI-01", "2.3.4"));
    }

    [Fact]
    public async Task JsonProfile_WithBrokenCatalogReference_IsRejectedBeforeApply()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "profileId": "broken",
              "interfaceVersion": "1.0.0",
              "reports": [ { "id": 2001, "variableIds": [9999] } ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        GemCatalogValidationException exception = await Assert.ThrowsAsync<GemCatalogValidationException>(
            () => GemInterfaceProfileLoader.LoadJsonAsync(stream));

        Assert.Contains(exception.Result.Issues, issue => issue.Code == "GEM003");
    }

    [Fact]
    public void ImmutableCatalog_AllowsRuntimeValueUpdates()
    {
        GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
            .AddVariable(new GemVariableDefinition(
                new GemVid(1001), "LotId", GemVariableKind.DataVariable, Format: SecsFormat.ASCII))
            .AddReport(new GemReportDefinition(new GemRptid(2001), [new GemVid(1001)]))
            .AddEvent(new GemCollectionEventDefinition(new GemCeid(3001), "LotStarted", [new GemRptid(2001)]))
            .Build();
        var registry = new GemRegistry(catalog);
        registry.SealConfiguration();
        registry.RegisterVariable(new GemVariable(1001, "LotId", Item.A("LOT-001")));

        Assert.Equal("LOT-001", registry.Data.GetRequiredVariable(new GemVid(1001)).Value.GetString());
        Assert.DoesNotContain((uint)3002, registry.Catalog.Events.Keys);
    }

    [Fact]
    public void BuildCatalog_WhenReportReferencesUndefinedVid_ThrowsValidationException()
    {
        var builder = new GemInterfaceCatalogBuilder();
        builder.AddReport(new GemReportDefinition(
            new GemRptid(2001),
            new[] { new GemVid(9999) }));

        GemCatalogValidationException exception = Assert.Throws<GemCatalogValidationException>(
            builder.Build);

        Assert.Contains(exception.Result.Issues, issue => issue.Code == "GEM003");
    }

    [Fact]
    public void RegisterVariable_WhenVidOrNameIsDuplicated_Throws()
    {
        var builder = new GemInterfaceCatalogBuilder();
        builder.AddVariable(new GemVariableDefinition(
            new GemVid(1001),
            "LotId",
            GemVariableKind.DataVariable));

        Assert.Throws<InvalidOperationException>(() => builder.AddVariable(
            new GemVariableDefinition(new GemVid(1001), "Other", GemVariableKind.DataVariable)));
        Assert.Throws<InvalidOperationException>(() => builder.AddVariable(
            new GemVariableDefinition(new GemVid(1002), "lotid", GemVariableKind.DataVariable)));
    }

    [Fact]
    public void EventReport_WhenRuntimeValueIsMissing_ThrowsInsteadOfWritingEmptyValue()
    {
        var data = new GemDataSnapshot();
        var reports = new[]
        {
            new GemReportDefinition(new GemRptid(2001), new[] { new GemVid(1001) })
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GemMessageFactory.EventReport(new GemCeid(3001), reports, data));

        Assert.Contains("VID 1001", exception.Message);
    }

    [Fact]
    public void RegistryHistory_UsesConfiguredBounds()
    {
        var registry = new GemRegistry(options: new GemRegistryOptions(
            MaximumAlarmHistory: 2,
            MaximumProcessProgramHistory: 2,
            MaximumTraceSamples: 2));

        registry.SetAlarm(new GemAlarm(1, "A1", GemAlarmState.Set));
        registry.SetAlarm(new GemAlarm(2, "A2", GemAlarmState.Set));
        registry.SetAlarm(new GemAlarm(3, "A3", GemAlarmState.Set));

        Assert.Equal(new uint[] { 2, 3 }, registry.AlarmHistory.Select(x => x.Alarm.AlarmId));
    }

    [Fact]
    public void CatalogExporter_ProducesReviewableMarkdown()
    {
        GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
            .AddVariable(new GemVariableDefinition(
                new GemVid(1001), "LotId", GemVariableKind.DataVariable, Format: SecsFormat.ASCII))
            .Build();

        string document = GemInterfaceCatalogExporter.ToMarkdown(catalog, "AOI-01");

        Assert.Contains("# AOI-01 GEM Interface", document);
        Assert.Contains("| 1001 | LotId | DataVariable | ASCII |", document);
    }

    [Fact]
    public void SealConfiguration_WhenCommandDefinitionHasNoHandler_Throws()
    {
        GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
            .AddRemoteCommand(new GemRemoteCommandDefinition(
                "START", Array.Empty<GemRemoteCommandParameterDefinition>()))
            .Build();
        var registry = new GemRegistry(catalog);

        GemCatalogValidationException exception = Assert.Throws<GemCatalogValidationException>(
            registry.SealConfiguration);

        Assert.Contains(exception.Result.Issues, issue => issue.Code == "GEM010");
        Assert.False(registry.IsConfigurationSealed);

        registry.RegisterCommand("START", (_, _) => Task.FromResult(
            new GemRemoteCommandResult(GemHostCommandAckCode.Accepted)));
        registry.SealConfiguration();

        Assert.True(registry.IsConfigurationSealed);
    }

    [Fact]
    public void MessageFactories_CloneBorrowedItems()
    {
        using Item variableValue = Item.A("LOT-001");
        using (SecsMessage message = GemMessageFactory.SelectedEquipmentStatusData(
                   [new GemVariable(1001, "LotId", variableValue)]))
        {
        }

        using Item commandValue = Item.A("PP-001");
        using (SecsMessage message = GemMessageFactory.RemoteCommand(
                   new GemRemoteCommand("START", new Dictionary<string, Item> { ["PPID"] = commandValue })))
        {
        }

        using var processProgram = new GemProcessProgram("PP-001", Item.A("BODY"));
        using (SecsMessage message = GemMessageFactory.ProcessProgramSend(processProgram))
        {
        }

        Assert.Equal("LOT-001", variableValue.GetString());
        Assert.Equal("PP-001", commandValue.GetString());
        Assert.Equal("BODY", processProgram.Body.GetString());
    }

    [Fact]
    public void S7F3Parser_ReturnsBodyIndependentFromIncomingMessage()
    {
        using var source = new GemProcessProgram("PP-001", Item.A("BODY"));
        GemParseResult<GemProcessProgram> parsed;
        using (SecsMessage message = GemMessageFactory.ProcessProgramSend(source))
        {
            parsed = new S7F3Parser().Parse(message);
        }

        Assert.True(parsed.IsSuccess);
        using GemProcessProgram result = parsed.Value!;
        Assert.Equal("BODY", result.Body.GetString());
    }

    [Fact]
    public void RemoteCommandValidator_ValidatesStateRequiredParameterAndFormat()
    {
        var definition = new GemRemoteCommandDefinition(
            "START",
            new[]
            {
                new GemRemoteCommandParameterDefinition("PPID", SecsFormat.ASCII)
            },
            new HashSet<GemControlState> { GemControlState.OnlineRemote });

        GemRemoteCommandValidationResult invalidState = GemRemoteCommandValidator.Validate(
            definition,
            new GemRemoteCommand("START", new Dictionary<string, Item> { ["PPID"] = Item.A("RCP-01") }),
            GemControlState.OnlineLocal);
        GemRemoteCommandValidationResult missingParameter = GemRemoteCommandValidator.Validate(
            definition,
            new GemRemoteCommand("START", new Dictionary<string, Item>()),
            GemControlState.OnlineRemote);
        GemRemoteCommandValidationResult invalidFormat = GemRemoteCommandValidator.Validate(
            definition,
            new GemRemoteCommand("START", new Dictionary<string, Item> { ["PPID"] = Item.U4(1) }),
            GemControlState.OnlineRemote);
        GemRemoteCommandValidationResult valid = GemRemoteCommandValidator.Validate(
            definition,
            new GemRemoteCommand("START", new Dictionary<string, Item> { ["ppid"] = Item.A("RCP-01") }),
            GemControlState.OnlineRemote);

        Assert.Equal(GemHostCommandAckCode.CannotPerformNow, invalidState.AckCode);
        Assert.Equal(GemHostCommandAckCode.InvalidParameter, missingParameter.AckCode);
        Assert.Equal(GemHostCommandAckCode.InvalidParameter, invalidFormat.AckCode);
        Assert.True(valid.IsValid);
    }
}
