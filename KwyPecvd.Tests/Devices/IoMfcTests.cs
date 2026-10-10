using Kwy.Device.IoCard.Abstractions;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Io;

namespace KwyPecvd.Tests.Devices;

public sealed class IoMfcTests
{
    [Fact]
    public async Task ExecuteCycleAsync_WritesEngineeringSetPointAndReadsFeedback()
    {
        var analog = new FakeAnalogIo { Feedback = 98.5 };
        var mfc = CreateMfc(analog);
        await mfc.InitializeAsync();
        await mfc.SetFlowAsync(100, TimeSpan.Zero);

        await mfc.ExecuteCycleAsync();

        Assert.Equal(100, analog.LastOutput);
        Assert.Equal("MFC_SIH4", analog.LastOwner);
        Assert.Equal(98.5, mfc.Snapshot.Feedback);
        Assert.False(mfc.Snapshot.IsOffline);
    }

    [Fact]
    public async Task InvalidAnalogQuality_MarksFeedbackInvalid()
    {
        var analog = new FakeAnalogIo
        {
            Feedback = double.NaN,
            Quality = AnalogIoQuality.OpenCircuit
        };
        var mfc = CreateMfc(analog);
        await mfc.InitializeAsync();

        await mfc.ExecuteCycleAsync();

        Assert.False(mfc.Snapshot.IsOffline);
        Assert.False(mfc.Snapshot.IsFeedbackValid);
        Assert.False(mfc.Snapshot.IsOutOfTolerance);
        Assert.Equal(
            MfcDiagnosticCodes.FeedbackBadQuality,
            mfc.Snapshot.DiagnosticCode);
    }

    [Fact]
    public async Task InitializeAsync_RejectsMismatchedEngineeringScale()
    {
        var analog = new FakeAnalogIo();
        var provider = new FakeAnalogPointProvider(engineeringMaximum: 1000);
        var mfc = CreateMfc(analog, provider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mfc.InitializeAsync());
    }

    private static IoMfc CreateMfc(
        FakeAnalogIo analog,
        FakeAnalogPointProvider? provider = null)
    {
        return new IoMfc(
            new IoMfcDefinition
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
                    ToleranceDelay = TimeSpan.FromSeconds(3)
                },
                SetPointAoId = "MFC.SIH4.SET_POINT",
                FeedbackAiId = "MFC.SIH4.FEEDBACK",
                OutputOwner = "MFC_SIH4"
            },
            provider ?? new FakeAnalogPointProvider(500),
            analog,
            analog);
    }

    private sealed class FakeAnalogIo :
        ILogicalAnalogInputReader,
        ILogicalAnalogOutputWriter
    {
        public double Feedback { get; init; }
        public AnalogIoQuality Quality { get; init; } = AnalogIoQuality.Good;
        public double LastOutput { get; private set; }
        public string? LastOwner { get; private set; }

        public ValueTask<double> ReadAsync(
            string pointId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Feedback);

        public ValueTask<AnalogIoSample> ReadSampleAsync(
            string pointId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AnalogIoSample(
                pointId,
                Feedback,
                Feedback,
                DateTimeOffset.UtcNow,
                Quality));

        public ValueTask WriteAsync(
            string pointId,
            double value,
            string? owner = null,
            CancellationToken cancellationToken = default)
        {
            LastOutput = value;
            LastOwner = owner;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeAnalogPointProvider : IAnalogIoPointDefinitionProvider
    {
        private readonly IReadOnlyDictionary<string, AnalogIoPointDefinition> points;

        public FakeAnalogPointProvider(double engineeringMaximum)
        {
            points = new Dictionary<string, AnalogIoPointDefinition>
            {
                ["MFC.SIH4.SET_POINT"] = CreatePoint(
                    "MFC.SIH4.SET_POINT", AnalogIoDirection.Output, engineeringMaximum),
                ["MFC.SIH4.FEEDBACK"] = CreatePoint(
                    "MFC.SIH4.FEEDBACK", AnalogIoDirection.Input, engineeringMaximum)
            };
        }

        public IReadOnlyCollection<AnalogIoPointDefinition> Definitions => points.Values.ToArray();

        public AnalogIoPointDefinition GetRequired(string pointId) =>
            points.TryGetValue(pointId, out var point)
                ? point
                : throw new KeyNotFoundException(pointId);

        public bool TryGet(string pointId, out AnalogIoPointDefinition definition) =>
            points.TryGetValue(pointId, out definition!);

        public IReadOnlyCollection<AnalogIoPointDefinition> GetByDirection(
            AnalogIoDirection direction) =>
            points.Values.Where(point => point.Direction == direction).ToArray();

        private static AnalogIoPointDefinition CreatePoint(
            string id,
            AnalogIoDirection direction,
            double engineeringMaximum) =>
            new()
            {
                Id = id,
                Name = id,
                DeviceId = "IO_MAIN",
                Direction = direction,
                Channel = 0,
                ElectricalSignal = AnalogElectricalSignal.Current,
                Scale = new AnalogIoScale(4, 20, 0, engineeringMaximum),
                Unit = "sccm",
                Owner = direction == AnalogIoDirection.Output ? "MFC_SIH4" : null,
                Criticality = IoPointCriticality.ProcessCritical
            };
    }
}
