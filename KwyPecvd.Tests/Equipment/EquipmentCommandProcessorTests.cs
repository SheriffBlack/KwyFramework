using KwyPecvd.Device;
using KwyPecvd.Equipment.Commands;
using KwyPecvd.Equipment.Events;
using KwyPecvd.Process.Chamber;
using KwyPecvd.Process.Chamber.Gas;
using KwyPecvd.Process.Modules;

namespace KwyPecvd.Tests.Equipment;

public sealed class EquipmentCommandProcessorTests
{
    [Fact]
    public async Task ExecuteAsync_InitializesRequestedChamber()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var chamber = new FakeChamberController("PM1");

        registry.Add(chamber);

        var pendingStore =
            new InMemoryPendingEquipmentCommandStore();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var processor =
            new EquipmentCommandProcessor(
                registry,
                pendingStore,
                resultStore);

        var envelope =
            new EquipmentCommandEnvelope(
                Context:
                    new EquipmentCommandContext(
                        RequestId: "request-001",
                        Source: EquipmentCommandSource.Diagnostic,
                        Requester: "test",
                        RequestedAt: DateTimeOffset.UtcNow),
                Command:
                    new InitializeEquipmentCommand(
                        "PM1"));

        // Act
        var result =
            await processor.ExecuteAsync(envelope);

        // Assert
        Assert.Equal(
            EquipmentCommandStatus.Completed,
            result.Status);

        Assert.Equal(1, chamber.InitializeCallCount);
        Assert.Equal("request-001", result.RequestId);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownModule()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var pendingStore =
          new InMemoryPendingEquipmentCommandStore();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var processor =
            new EquipmentCommandProcessor(
                registry,
                pendingStore,
                resultStore);

        var envelope =
            new EquipmentCommandEnvelope(
                Context:
                    new EquipmentCommandContext(
                        RequestId: "request-002",
                        Source: EquipmentCommandSource.Diagnostic,
                        Requester: "test",
                        RequestedAt: DateTimeOffset.UtcNow),
                Command:
                    new InitializeEquipmentCommand(
                        "UNKNOWN"));

        // Act
        var result =
            await processor.ExecuteAsync(envelope);

        // Assert
        Assert.Equal(
            EquipmentCommandStatus.Rejected,
            result.Status);

        Assert.Equal(
            EquipmentCommandResultCodes.ModuleNotFound,
            result.Code);
    }

    [Fact]
    public async Task ExecuteAsync_ResetsRequestedChamberFault()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var chamber = new FakeChamberController("PM1");

        registry.Add(chamber);

        var pendingStore =
            new InMemoryPendingEquipmentCommandStore();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var processor =
            new EquipmentCommandProcessor(
                registry,
                pendingStore,
                resultStore);

        var envelope =
            new EquipmentCommandEnvelope(
                Context:
                    new EquipmentCommandContext(
                        RequestId: "reset-001",
                        Source: EquipmentCommandSource.Diagnostic,
                        Requester: "test",
                        RequestedAt: DateTimeOffset.UtcNow),
                Command:
                    new ResetEquipmentFaultCommand(
                        "PM1"));

        // Act
        var result =
            await processor.ExecuteAsync(envelope);

        // Assert
        Assert.Equal(
            EquipmentCommandStatus.Completed,
            result.Status);

        Assert.Equal(
            "reset-001",
            result.RequestId);

        Assert.Equal(
            1,
            chamber.ResetFaultCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsStopWithoutReportingCompletion()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var chamber = new FakeChamberController("PM1");

        registry.Add(chamber);

        var pendingStore =
            new InMemoryPendingEquipmentCommandStore();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var processor =
            new EquipmentCommandProcessor(
                registry,
                pendingStore,
                resultStore);

        var envelope =
            new EquipmentCommandEnvelope(
                Context:
                    new EquipmentCommandContext(
                        RequestId: "stop-001",
                        Source: EquipmentCommandSource.Diagnostic,
                        Requester: "test",
                        RequestedAt: DateTimeOffset.UtcNow),
                Command:
                    new StopProcessCommand("PM1"));

        // Act
        var result =
            await processor.ExecuteAsync(envelope);

        // Assert
        Assert.Equal(
            EquipmentCommandStatus.Accepted,
            result.Status);

        Assert.Equal(
            EquipmentCommandResultCodes.Accepted,
            result.Code);

        Assert.Equal(
            "stop-001",
            result.RequestId);

        Assert.Equal(
            1,
            chamber.StopProcessCallCount);

        Assert.True(
            resultStore.TryGet(
                "stop-001",
                out var storedResult));

        Assert.NotNull(storedResult);

        Assert.Equal(
            EquipmentCommandStatus.Accepted,
            storedResult.Status);

        Assert.Equal(
            EquipmentCommandTypeCodes.StopProcess,
            storedResult.CommandTypeCode);

        Assert.Equal(
            "PM1",
            storedResult.EquipmentModuleId);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsSecondPendingStop()
    {
        // Arrange
        var registry = new EquipmentModuleRegistry();

        var chamber = new FakeChamberController("PM1");

        registry.Add(chamber);

        var pendingStore =
            new InMemoryPendingEquipmentCommandStore();


        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var processor =
            new EquipmentCommandProcessor(
                registry,
                pendingStore,
                resultStore);

        var first =
            new EquipmentCommandEnvelope(
                Context:
                    new EquipmentCommandContext(
                        RequestId: "stop-001",
                        Source: EquipmentCommandSource.Diagnostic,
                        Requester: "test",
                        RequestedAt: DateTimeOffset.UtcNow),
                Command:
                    new StopProcessCommand("PM1"));

        var second =
            new EquipmentCommandEnvelope(
                Context:
                    new EquipmentCommandContext(
                        RequestId: "stop-002",
                        Source: EquipmentCommandSource.Diagnostic,
                        Requester: "test",
                        RequestedAt: DateTimeOffset.UtcNow),
                Command:
                    new StopProcessCommand("PM1"));

        // Act
        var firstResult =
            await processor.ExecuteAsync(first);

        var secondResult =
            await processor.ExecuteAsync(second);

        // Assert
        Assert.Equal(
            EquipmentCommandStatus.Accepted,
            firstResult.Status);

        Assert.Equal(
            EquipmentCommandStatus.Rejected,
            secondResult.Status);

        Assert.Equal(
            EquipmentCommandResultCodes.AlreadyPending,
            secondResult.Code);

        Assert.Equal(
            1,
            chamber.StopProcessCallCount);
    }

    [Fact]
    public async Task ObserveAsync_KeepsStopPendingWhileChamberIsStopping()
    {
        var store =
            new InMemoryPendingEquipmentCommandStore();

        store.TryAdd(CreatePendingStop("stop-001"));

        var publisher =
            new FakeEquipmentEventPublisher();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var observer =
            new EquipmentCommandCompletionObserver(
                store,
                resultStore,
                publisher);

        var chamber =
            new FakeChamberController("PM1");

        chamber.SetState(ChamberState.Stopping);

        await observer.ObserveAsync(chamber);

        Assert.Empty(publisher.Events);

        Assert.False(
            store.TryAdd(CreatePendingStop("stop-002")));
    }

    [Fact]
    public async Task ObserveAsync_CompletesPendingStopOnlyOnceWhenChamberIsIdle()
    {
        var store =
            new InMemoryPendingEquipmentCommandStore();

        store.TryAdd(CreatePendingStop("stop-001"));

        var publisher =
            new FakeEquipmentEventPublisher();

        var resultStore =
           new InMemoryEquipmentCommandResultStore();

        var observer =
            new EquipmentCommandCompletionObserver(
                store,
                resultStore,
                publisher);

        var chamber =
            new FakeChamberController("PM1");

        chamber.SetState(ChamberState.Idle);

        // 模拟两个连续的100ms RT周期。
        await observer.ObserveAsync(chamber);
        await observer.ObserveAsync(chamber);

        var equipmentEvent =
            Assert.Single(publisher.Events);

        Assert.Equal(
            EquipmentEventCodes.ProcessStopped,
            equipmentEvent.Code);

        Assert.Equal(
            EquipmentCommandStatus.Completed,
            equipmentEvent.CommandStatus);

        Assert.Equal(
            "stop-001",
            equipmentEvent.CommandContext?.RequestId);

        Assert.True(
            resultStore.TryGet(
                "stop-001",
                out var storedResult));

        Assert.NotNull(storedResult);

        Assert.Equal(
            EquipmentCommandStatus.Completed,
            storedResult.Status);

        Assert.True(
            store.TryAdd(CreatePendingStop("stop-002")));
    }

    [Fact]
    public async Task ObserveAsync_FailsPendingStopWhenChamberIsFaulted()
    {
        var store =
            new InMemoryPendingEquipmentCommandStore();

        store.TryAdd(CreatePendingStop("stop-001"));

        var publisher =
            new FakeEquipmentEventPublisher();

        var resultStore =
             new InMemoryEquipmentCommandResultStore();

        var observer =
            new EquipmentCommandCompletionObserver(
                store,
                resultStore,
                publisher);

        var chamber =
            new FakeChamberController("PM1");

        chamber.SetState(
            ChamberState.Faulted,
            "PROCESS_STOP_TIMEOUT",
            "Process stop timed out.");

        await observer.ObserveAsync(chamber);

        var equipmentEvent =
            Assert.Single(publisher.Events);

        Assert.Equal(
            EquipmentEventCodes.ProcessStopFailed,
            equipmentEvent.Code);

        Assert.Equal(
            EquipmentCommandStatus.Failed,
            equipmentEvent.CommandStatus);

        Assert.Equal(
            "PROCESS_STOP_TIMEOUT",
            equipmentEvent.ResultCode);

        Assert.Equal(
            "stop-001",
            equipmentEvent.CommandContext?.RequestId);
    }

    [Fact]
    public async Task ObserveAsync_RestoresPendingWhenEventPublishingFails()
    {
        // Arrange
        var store = new InMemoryPendingEquipmentCommandStore();

        store.TryAdd(
            CreatePendingStop("stop-001"));

        var publisher =
            new FakeEquipmentEventPublisher();

        publisher.FailNextPublish();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var observer =
            new EquipmentCommandCompletionObserver(
                store,
                resultStore,
                publisher);

        var chamber =
            new FakeChamberController("PM1");

        chamber.SetState(ChamberState.Idle);

        // Act：第一次观察时，事件发布失败。
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => observer.ObserveAsync(chamber));

        // Assert：原 Pending 已恢复，因此第二条 Stop 仍应被拒绝。
        Assert.False(
            store.TryAdd(
                CreatePendingStop("stop-002")));

        Assert.Empty(publisher.Events);

        // Act：模拟下一个100ms周期，Publisher 已经恢复。
        await observer.ObserveAsync(chamber);

        // Assert：原命令最终成功发布。
        var equipmentEvent =
            Assert.Single(publisher.Events);

        Assert.Equal(
            "stop-001",
            equipmentEvent.CommandContext?.RequestId);

        Assert.Equal(
            EquipmentCommandStatus.Completed,
            equipmentEvent.CommandStatus);

        Assert.Equal(
            2,
            publisher.PublishCallCount);

        // 原 Pending 已成功完成，现在可以接收下一条 Stop。
        Assert.True(
            store.TryAdd(
                CreatePendingStop("stop-002")));
    }

    [Fact]
    public void ResultStore_DoesNotReplaceCompletedWithAccepted()
    {
        var store =
            new InMemoryEquipmentCommandResultStore();

        var completedAt = DateTimeOffset.UtcNow;

        store.Set(
            new EquipmentCommandResultSnapshot(
                RequestId: "stop-001",
                CommandTypeCode:
                    EquipmentCommandTypeCodes.StopProcess,
                EquipmentModuleId: "PM1",
                Status: EquipmentCommandStatus.Completed,
                Code: EquipmentCommandResultCodes.Completed,
                Message: "Process stopped.",
                UpdatedAt: completedAt));

        store.Set(
            new EquipmentCommandResultSnapshot(
                RequestId: "stop-001",
                CommandTypeCode:
                    EquipmentCommandTypeCodes.StopProcess,
                EquipmentModuleId: "PM1",
                Status: EquipmentCommandStatus.Accepted,
                Code: EquipmentCommandResultCodes.Accepted,
                Message: "Stop accepted.",
                UpdatedAt: completedAt.AddMilliseconds(1)));

        Assert.True(
            store.TryGet(
                "stop-001",
                out var result));

        Assert.NotNull(result);

        Assert.Equal(
            EquipmentCommandStatus.Completed,
            result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsRejectedWhenStopIsCanceled()
    {
        var registry =
            new EquipmentModuleRegistry();

        var chamber =
            new FakeChamberController("PM1")
            {
                CancelStopProcess = true
            };

        registry.Add(chamber);

        var pendingStore =
            new InMemoryPendingEquipmentCommandStore();

        var resultStore =
            new InMemoryEquipmentCommandResultStore();

        var processor =
            new EquipmentCommandProcessor(
                registry,
                pendingStore,
                resultStore);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var envelope =
            new EquipmentCommandEnvelope(
                new EquipmentCommandContext(
                    RequestId: "stop-canceled-001",
                    Source:
                        EquipmentCommandSource.Diagnostic,
                    Requester:
                        "test",
                    RequestedAt:
                        DateTimeOffset.UtcNow),
                new StopProcessCommand("PM1"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ExecuteAsync(
                envelope,
                cancellation.Token));

        Assert.True(
            resultStore.TryGet(
                "stop-canceled-001",
                out var result));

        Assert.NotNull(result);

        Assert.Equal(
            EquipmentCommandStatus.Rejected,
            result.Status);

        Assert.Equal(
            EquipmentCommandResultCodes.Canceled,
            result.Code);

        // Pending已撤销，同一模块可以再次提交Stop。
        Assert.True(
            pendingStore.TryAdd(
                CreatePendingStop("stop-002")));
    }

    private static PendingEquipmentCommand CreatePendingStop(string requestId)
    {
        return new PendingEquipmentCommand(
            Context:
                new EquipmentCommandContext(
                    RequestId: requestId,
                    Source: EquipmentCommandSource.Diagnostic,
                    Requester: "test",
                    RequestedAt: DateTimeOffset.UtcNow),
            CommandTypeCode:
                EquipmentCommandTypeCodes.StopProcess,
            EquipmentModuleId:
                "PM1",
            TrackedAt:
                DateTimeOffset.UtcNow);
    }

    private static EquipmentCommandProcessor CreateProcessor(
        IEquipmentModuleRegistry registry,
        out InMemoryPendingEquipmentCommandStore pendingStore)
    {
        pendingStore =
            new InMemoryPendingEquipmentCommandStore();

        return new EquipmentCommandProcessor(
            registry,
            pendingStore,
            new InMemoryEquipmentCommandResultStore());
    }


    private sealed class FakeChamberController : IChamberController
    {
        public FakeChamberController(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public bool CancelStopProcess { get; set; }

        public ChamberDefinition Definition => null!;

        public ChamberSnapshot Snapshot { get; set; } = CreateSnapshot(ChamberState.Idle);

        public int InitializeCallCount { get; private set; }

        public int ResetFaultCallCount { get; private set; }

        public int StopProcessCallCount { get; private set; }

        public Task<CommandResult> InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;

            return Task.FromResult(
                CommandResult.Success(
                    $"Chamber '{Id}' initialized."));
        }

        public Task<CommandResult> SetGasFlowAsync(
            string gasName,
            double target,
            TimeSpan rampDuration,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CommandResult> StopGasFlowAsync(
            string gasName,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CommandResult> StopProcessAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            StopProcessCallCount++;

            return Task.FromResult(
                CommandResult.Success(
                    $"Chamber '{Id}' accepted stop."));
        }

        public Task<CommandResult> StartGasPreparationAsync(
                IReadOnlyCollection<
                    ChamberGasTarget> targets,
                CancellationToken
                    cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CommandResult> StartPumpDownAsync(
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<CommandResult> ResetFaultAsync(
            CancellationToken cancellationToken = default)
        {
            ResetFaultCallCount++;

            return Task.FromResult(
                CommandResult.Success(
                    $"Chamber '{Id}' fault was reset."));
        }

        public Task ExecuteCycleAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void SetState(
            ChamberState state,
            string? faultCode = null,
            string? faultMessage = null)
        {
            Snapshot = CreateSnapshot(
                state,
                faultCode,
                faultMessage);
        }

        private static ChamberSnapshot CreateSnapshot(
            ChamberState state,
            string? faultCode = null,
            string? faultMessage = null)
        {
            return new ChamberSnapshot
            {
                ChamberId = "PM1",
                State = state,
                StateChangedAt = DateTimeOffset.UtcNow,
                FaultCode = faultCode,
                FaultMessage = faultMessage,
                Vacuum = null!,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
    }

    private sealed class FakeEquipmentEventPublisher : IEquipmentEventPublisher
    {
        private readonly List<EquipmentEvent> events = [];

        private bool failNextPublish;

        public IReadOnlyList<EquipmentEvent> Events => events;

        public int PublishCallCount { get; private set; }

        public void FailNextPublish()
        {
            failNextPublish = true;
        }

        public Task PublishAsync(
            EquipmentEvent equipmentEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PublishCallCount++;

            if (failNextPublish)
            {
                failNextPublish = false;

                throw new InvalidOperationException(
                    "Simulated event publishing failure.");
            }

            events.Add(equipmentEvent);

            return Task.CompletedTask;
        }
    }
}
