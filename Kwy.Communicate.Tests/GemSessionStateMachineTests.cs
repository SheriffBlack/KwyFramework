using Kwy.Communicate.Gem;
using Kwy.Communicate.Gem.Testing;
using Secs4Net;

namespace Kwy.Communicate.Tests;

public sealed class GemSessionStateMachineTests
{
    [Fact]
    public void CompleteOnline_WithoutCommunication_Throws()
    {
        var stateMachine = new GemSessionStateMachine();

        Assert.Throws<InvalidOperationException>(() => stateMachine.BeginAttemptOnline());
        Assert.Equal(GemCommunicationState.NotCommunicating, stateMachine.CommunicationState);
        Assert.Equal(GemControlState.Offline, stateMachine.ControlState);
    }

    [Fact]
    public void OnlineRemote_FollowsExpectedTransitionPath()
    {
        var stateMachine = new GemSessionStateMachine();

        stateMachine.MarkCommunicating();
        stateMachine.BeginAttemptOnline();
        stateMachine.CompleteOnline(remote: true);

        Assert.Equal(GemCommunicationState.Communicating, stateMachine.CommunicationState);
        Assert.Equal(GemControlState.OnlineRemote, stateMachine.ControlState);
    }

    [Fact]
    public void CommunicationLoss_ReturnsControlStateToOffline()
    {
        var stateMachine = new GemSessionStateMachine();
        stateMachine.MarkCommunicating();
        stateMachine.BeginAttemptOnline();
        stateMachine.CompleteOnline(remote: true);

        stateMachine.MarkCommunicationLost();

        Assert.Equal(GemCommunicationState.NotCommunicating, stateMachine.CommunicationState);
        Assert.Equal(GemControlState.Offline, stateMachine.ControlState);
    }

    [Fact]
    public void StateChanged_ContainsPreviousAndCurrentState()
    {
        var stateMachine = new GemSessionStateMachine();
        GemSessionStateChangedEventArgs? observed = null;
        stateMachine.StateChanged += (_, args) => observed = args;

        stateMachine.MarkCommunicating("test");

        Assert.NotNull(observed);
        Assert.Equal(GemCommunicationState.NotCommunicating, observed.PreviousCommunicationState);
        Assert.Equal(GemCommunicationState.Communicating, observed.CommunicationState);
        Assert.Equal("test", observed.Reason);
    }

    [Fact]
    public async Task S1F13Handler_AcceptsCommunicationAndReturnsS1F14()
    {
        var session = new TestEquipmentSession();
        using var request = new SecsMessage(1, 13, true);

        using SecsMessage? reply = await new S1F13Handler(session).HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal((byte)1, reply.S);
        Assert.Equal((byte)14, reply.F);
        Assert.Equal(GemCommunicationState.Communicating, session.CommunicationState);
        Assert.Equal((byte)0, reply.SecsItem![0].FirstValue<byte>());
    }

    [Fact]
    public async Task S1F17Handler_WhenAuthorized_EntersOnlineLocal()
    {
        var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        using var request = new SecsMessage(1, 17, true);
        var handler = new S1F17Handler(session, _ => Task.FromResult(true));

        using SecsMessage? reply = await handler.HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal((byte)18, reply.F);
        Assert.Equal((byte)0, reply.SecsItem!.FirstValue<byte>());
        Assert.Equal(GemControlState.OnlineLocal, session.ControlState);
    }

    [Fact]
    public async Task S1F17Handler_WhenDenied_RemainsOffline()
    {
        var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        using var request = new SecsMessage(1, 17, true);
        var handler = new S1F17Handler(session, _ => Task.FromResult(false));

        using SecsMessage? reply = await handler.HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal((byte)1, reply.SecsItem!.FirstValue<byte>());
        Assert.Equal(GemControlState.Offline, session.ControlState);
    }

    [Fact]
    public async Task S1F15Handler_WhenOnline_EntersHostOfflineAndReturnsS1F16()
    {
        var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        await session.AcceptHostOnlineRequestAsync(remote: true);
        using var request = new SecsMessage(1, 15, true);

        using SecsMessage? reply = await new S1F15Handler(session).HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal((byte)1, reply.S);
        Assert.Equal((byte)16, reply.F);
        Assert.Equal((byte)GemOfflineAcknowledgeCode.Acknowledged, reply.SecsItem!.FirstValue<byte>());
        Assert.Equal(GemCommunicationState.Communicating, session.CommunicationState);
        Assert.Equal(GemControlState.HostOffline, session.ControlState);
    }

    [Fact]
    public void AcceptHostOfflineRequest_WithoutCommunication_Throws()
    {
        var stateMachine = new GemSessionStateMachine();

        Assert.Throws<InvalidOperationException>(() => stateMachine.AcceptHostOfflineRequest());
        Assert.Equal(GemControlState.Offline, stateMachine.ControlState);
    }

    [Fact]
    public void AcceptHostOfflineRequest_WhenAlreadyEquipmentOffline_RemainsOffline()
    {
        var stateMachine = new GemSessionStateMachine();
        stateMachine.MarkCommunicating();

        stateMachine.AcceptHostOfflineRequest();

        Assert.Equal(GemCommunicationState.Communicating, stateMachine.CommunicationState);
        Assert.Equal(GemControlState.Offline, stateMachine.ControlState);
    }

    [Fact]
    public async Task S1F11Handler_ReturnsRequestedStatusVariablesAndUnknownPlaceholderInOrder()
    {
        GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
            .AddVariable(new(new GemVid(1001), "LotId", GemVariableKind.DataVariable))
            .AddVariable(new(new GemVid(1002), "ControlState", GemVariableKind.StatusVariable))
            .AddVariable(new(new GemVid(1003), "Temperature", GemVariableKind.StatusVariable, "degC"))
            .Build();
        var registry = new GemRegistry(catalog);
        var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        using var request = new SecsMessage(1, 11, true)
        {
            SecsItem = Item.L(Item.U4(1003), Item.U4(9999), Item.U4(1001))
        };

        using SecsMessage? reply = await new S1F11Handler(registry, session).HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal((byte)12, reply.F);
        Assert.Equal(3, reply.SecsItem!.Count);
        Assert.Equal((uint)1003, reply.SecsItem[0][0].FirstValue<uint>());
        Assert.Equal("Temperature", reply.SecsItem[0][1].GetString());
        Assert.Equal("degC", reply.SecsItem[0][2].GetString());
        Assert.Equal((uint)9999, reply.SecsItem[1][0].FirstValue<uint>());
        Assert.Equal(string.Empty, reply.SecsItem[1][1].GetString());
        Assert.Equal(string.Empty, reply.SecsItem[1][2].GetString());
        Assert.Equal((uint)1001, reply.SecsItem[2][0].FirstValue<uint>());
        Assert.Equal(string.Empty, reply.SecsItem[2][1].GetString());
    }

    [Fact]
    public async Task S1F11Handler_WithEmptyList_ReturnsAllStatusVariablesSortedBySvid()
    {
        GemInterfaceCatalog catalog = new GemInterfaceCatalogBuilder()
            .AddVariable(new(new GemVid(1003), "Temperature", GemVariableKind.StatusVariable, "degC"))
            .AddVariable(new(new GemVid(1001), "LotId", GemVariableKind.DataVariable))
            .AddVariable(new(new GemVid(1002), "ControlState", GemVariableKind.StatusVariable))
            .Build();
        var registry = new GemRegistry(catalog);
        var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        using var request = GemMessageFactory.StatusVariableNamelistRequest();

        using SecsMessage? reply = await new S1F11Handler(registry, session).HandleAsync(request);

        Assert.NotNull(reply);
        Assert.Equal(2, reply.SecsItem!.Count);
        Assert.Equal((uint)1002, reply.SecsItem[0][0].FirstValue<uint>());
        Assert.Equal((uint)1003, reply.SecsItem[1][0].FirstValue<uint>());
    }

    [Fact]
    public async Task Stream7Handlers_ProvideReadDirectoryAndDeleteRoundTrip()
    {
        using var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        await session.AcceptHostOnlineRequestAsync(remote: true);
        using (var source = new GemProcessProgram("RCP-01", Item.A("BODY")))
            await session.SaveProcessProgramAsync(source);

        using var requestProgram = new SecsMessage(7, 5, true) { SecsItem = Item.A("RCP-01") };
        using SecsMessage? programData = await new S7F5Handler(session).HandleAsync(requestProgram);
        Assert.NotNull(programData);
        Assert.Equal((byte)6, programData.F);
        Assert.Equal("RCP-01", programData.SecsItem![0].GetString());
        Assert.Equal("BODY", programData.SecsItem[1].GetString());

        using var requestDirectory = new SecsMessage(7, 19, true);
        using SecsMessage? directory = await new S7F19Handler(session).HandleAsync(requestDirectory);
        Assert.Equal("RCP-01", directory!.SecsItem![0].GetString());

        using var delete = new SecsMessage(7, 17, true) { SecsItem = Item.L(Item.A("RCP-01")) };
        using SecsMessage? deleteAck = await new S7F17Handler(session).HandleAsync(delete);
        Assert.Equal((byte)0, deleteAck!.SecsItem!.FirstValue<byte>());
        Assert.Empty(await session.ListProcessProgramIdsAsync());
    }

    [Fact]
    public async Task S7F1Handler_UsesRepositoryLoadPolicy()
    {
        using var repository = new InMemoryGemProcessProgramRepository();
        var loadPolicy = new RepositoryGemProcessProgramLoadPolicy(
            repository,
            maximumBodyLength: 1024);
        using var session = new TestEquipmentSession();
        await session.AcceptCommunicationAsync();
        await session.AcceptHostOnlineRequestAsync(remote: true);
        var handler = new S7F1Handler(session, loadPolicy);

        using var acceptedRequest = new SecsMessage(7, 1, true)
        {
            SecsItem = Item.L(Item.A("RCP-01"), Item.U4(100))
        };
        using SecsMessage? accepted = await handler.HandleAsync(acceptedRequest);
        Assert.Equal((byte)GemProcessProgramLoadGrant.Granted, accepted!.SecsItem!.FirstValue<byte>());

        using var tooLargeRequest = new SecsMessage(7, 1, true)
        {
            SecsItem = Item.L(Item.A("RCP-02"), Item.U4(2048))
        };
        using SecsMessage? tooLarge = await handler.HandleAsync(tooLargeRequest);
        Assert.Equal((byte)GemProcessProgramLoadGrant.NoSpace, tooLarge!.SecsItem!.FirstValue<byte>());

        using var existing = new GemProcessProgram("RCP-03", Item.A("BODY"));
        await repository.SaveAsync(existing);
        using var duplicateRequest = new SecsMessage(7, 1, true)
        {
            SecsItem = Item.L(Item.A("RCP-03"), Item.U4(4))
        };
        using SecsMessage? duplicate = await handler.HandleAsync(duplicateRequest);
        Assert.Equal((byte)GemProcessProgramLoadGrant.AlreadyHave, duplicate!.SecsItem!.FirstValue<byte>());
    }

    [Fact]
    public void AcceptedRemoteCommand_IsNotInferredAsCompleted()
    {
        var accepted = new GemRemoteCommandResult(GemHostCommandAckCode.Accepted);

        GemRemoteCommandResult normalized = accepted.WithInferredCompletionStatus();

        Assert.Equal(4, (byte)GemHostCommandAckCode.Accepted);
        Assert.Equal(GemRemoteCommandCompletionStatus.Accepted, normalized.CompletionStatus);
        Assert.False(normalized.IsCompleted);

        GemRemoteCommandResult completed = new GemRemoteCommandResult(
            GemHostCommandAckCode.Completed).WithInferredCompletionStatus();
        Assert.Equal(GemRemoteCommandCompletionStatus.Completed, completed.CompletionStatus);
        Assert.True(completed.IsCompleted);
    }

    private sealed class TestEquipmentSession : IGemEquipmentSession, IDisposable
    {
        private readonly InMemoryGemProcessProgramRepository repository = new();
        public GemSessionStateMachine StateMachine { get; } = new();
        public GemCommunicationState CommunicationState => StateMachine.CommunicationState;
        public GemControlState ControlState => StateMachine.ControlState;
        public GemCommunicationContext Context => new(
            new GemEndpoint(GemHostRole.Equipment, "TEST", "MODEL", "1.0"),
            new GemEndpoint(GemHostRole.Host, "HOST"),
            CommunicationState,
            ControlState);
        public int SpooledMessageCount => 0;

        public Task EstablishCommunicationAsync(CancellationToken cancellationToken = default)
            => AcceptCommunicationAsync(cancellationToken);

        public Task AcceptCommunicationAsync(CancellationToken cancellationToken = default)
        {
            StateMachine.MarkCommunicating();
            return Task.CompletedTask;
        }

        public Task SetOnlineAsync(bool remote, CancellationToken cancellationToken = default)
            => AcceptHostOnlineRequestAsync(remote, cancellationToken);

        public Task AcceptHostOnlineRequestAsync(bool remote, CancellationToken cancellationToken = default)
        {
            StateMachine.BeginAttemptOnline();
            StateMachine.CompleteOnline(remote);
            return Task.CompletedTask;
        }

        public Task AcceptHostOfflineRequestAsync(CancellationToken cancellationToken = default)
        {
            StateMachine.AcceptHostOfflineRequest();
            return Task.CompletedTask;
        }

        public Task SetOfflineAsync(CancellationToken cancellationToken = default)
        {
            StateMachine.SetOffline();
            return Task.CompletedTask;
        }

        public Task ReportAlarmAsync(GemAlarm alarm, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReportEventAsync(uint eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendTerminalMessageAsync(GemTerminalMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GemProcessProgramSaveResult> SaveProcessProgramAsync(GemProcessProgram processProgram, GemProcessProgramSaveOptions? options = null, CancellationToken cancellationToken = default) => repository.SaveAsync(processProgram, options, cancellationToken);
        public Task<GemProcessProgram?> FindProcessProgramAsync(string ppid, CancellationToken cancellationToken = default) => repository.FindAsync(ppid, cancellationToken);
        public Task<IReadOnlyList<string>> ListProcessProgramIdsAsync(CancellationToken cancellationToken = default) => repository.ListPpidsAsync(cancellationToken);
        public Task<GemProcessProgramDeleteResult> DeleteProcessProgramAsync(string ppid, CancellationToken cancellationToken = default) => repository.DeleteAsync(ppid, cancellationToken);
        public Task<GemTraceSample> CaptureTraceAsync(uint traceId, uint sampleNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GemTraceRunResult> RunTraceAsync(uint traceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GemRemoteCommandResult> ExecuteRemoteCommandAsync(GemRemoteCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ReportRemoteCommandStatusAsync(string commandName, GemRemoteCommandCompletionStatus status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GemSpoolReplayResult> ReplaySpooledMessagesAsync(int maximumMessages = int.MaxValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GemSpoolReplayResult> RecoverCommunicationAsync(int maximumMessages = int.MaxValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() => repository.Dispose();
    }
}
