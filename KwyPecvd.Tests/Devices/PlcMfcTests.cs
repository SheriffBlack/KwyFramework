using Kwy.Device.PLC.Abstractions;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Plc;

namespace KwyPecvd.Tests.Devices;

public sealed class PlcMfcTests
{
    [Fact]
    public async Task ExecuteCycleAsync_SerializesNewCommandsWithPlcIo()
    {
        var transport = new FakePlcTransport { PauseWrites = true };
        var mfc = CreateMfc(transport);
        await mfc.InitializeAsync();

        var cycleTask = mfc.ExecuteCycleAsync();
        await transport.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var commandTask = mfc.SetFlowAsync(100, TimeSpan.Zero);
        Assert.False(commandTask.IsCompleted);

        transport.AllowWriteToComplete.TrySetResult();
        await cycleTask;
        var result = await commandTask;

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SnapshotObserverException_DoesNotMarkPlcOffline()
    {
        var transport = new FakePlcTransport();
        var mfc = CreateMfc(transport);
        await mfc.InitializeAsync();
        mfc.SnapshotChanged += (_, _) => throw new InvalidOperationException("Observer failed.");

        var exception = await Record.ExceptionAsync(() => mfc.ExecuteCycleAsync());

        Assert.Null(exception);
        Assert.False(mfc.Snapshot.IsOffline);
    }

    [Fact]
    public async Task CommunicationFailure_MarksPlcOfflineAndPropagatesError()
    {
        var transport = new FakePlcTransport { FailRead = true };
        var mfc = CreateMfc(transport);
        await mfc.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mfc.ExecuteCycleAsync());

        Assert.True(mfc.Snapshot.IsOffline);
    }

    private static PlcMfc CreateMfc(FakePlcTransport transport)
    {
        var definition = new PlcMfcDefinition
        {
            Mfc = new MfcDefinition
            {
                Id = "MFC_SIH4",
                ModuleId = "PM1",
                DisplayName = "SiH4 MFC",
                GasName = "SiH4",
                Unit = "sccm",
                FullScale = 500,
                Tolerance = 2,
                ToleranceDelay = TimeSpan.FromMilliseconds(30)
            },
            SetPointId = "MFC.SIH4.SET_POINT",
            FeedbackId = "MFC.SIH4.FEEDBACK",
            OfflineId = "MFC.SIH4.OFFLINE"
        };

        return new PlcMfc(
            definition,
            new FakePointProvider(),
            transport,
            transport);
    }

    private sealed class FakePlcTransport : ILogicalPlcReader, ILogicalPlcWriter
    {
        public bool PauseWrites { get; init; }
        public bool FailRead { get; init; }
        public TaskCompletionSource WriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowWriteToComplete { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WriteAsync<T>(
            string pointId,
            T value,
            CancellationToken cancellationToken = default)
        {
            WriteStarted.TrySetResult();
            if (PauseWrites)
                await AllowWriteToComplete.Task.WaitAsync(cancellationToken);
        }

        public Task<T> ReadAsync<T>(
            string pointId,
            CancellationToken cancellationToken = default)
        {
            if (FailRead)
                throw new InvalidOperationException("PLC read failed.");

            object value = pointId.EndsWith("OFFLINE", StringComparison.Ordinal)
                ? false
                : 0f;

            return Task.FromResult((T)value);
        }
    }

    private sealed class FakePointProvider : IPlcPointDefinitionProvider
    {
        private readonly IReadOnlyDictionary<string, PlcPointDefinition> points =
            new Dictionary<string, PlcPointDefinition>(StringComparer.Ordinal)
            {
                ["MFC.SIH4.SET_POINT"] = CreatePoint(
                    "MFC.SIH4.SET_POINT",
                    PlcDataType.Float,
                    PlcPointAccess.WriteOnly),
                ["MFC.SIH4.FEEDBACK"] = CreatePoint(
                    "MFC.SIH4.FEEDBACK",
                    PlcDataType.Float,
                    PlcPointAccess.ReadOnly),
                ["MFC.SIH4.OFFLINE"] = CreatePoint(
                    "MFC.SIH4.OFFLINE",
                    PlcDataType.Boolean,
                    PlcPointAccess.ReadOnly)
            };

        public IReadOnlyCollection<PlcPointDefinition> Definitions =>
            points.Values.ToArray();

        public bool TryGet(string pointId, out PlcPointDefinition point) =>
            points.TryGetValue(pointId, out point!);

        public PlcPointDefinition GetRequired(string pointId) =>
            points.TryGetValue(pointId, out var point)
                ? point
                : throw new KeyNotFoundException(pointId);

        private static PlcPointDefinition CreatePoint(
            string id,
            PlcDataType dataType,
            PlcPointAccess access)
        {
            return new PlcPointDefinition
            {
                Id = id,
                Name = id,
                DeviceId = "PLC_MAIN",
                Address = id,
                DataType = dataType,
                Access = access
            };
        }
    }
}
