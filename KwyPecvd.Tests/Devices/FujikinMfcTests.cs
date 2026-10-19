using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Fujikin;

namespace KwyPecvd.Tests.Devices;

public sealed class FujikinMfcTests
{
    [Fact]
    public async Task ExecuteCycleAsync_ConvertsEngineeringValueToRawValue()
    {
        var bus = new FakeFujikinMfcBus
        {
            RawFeedback = 2_500
        };
        var mfc = CreateMfc(bus);

        await mfc.InitializeAsync();
        await mfc.SetFlowAsync(250, TimeSpan.Zero);
        await mfc.ExecuteCycleAsync();

        Assert.Equal<short?>(5_000, bus.LastRawSetPoint);
        Assert.Equal(125, mfc.Snapshot.Feedback);
        Assert.True(mfc.Snapshot.IsFeedbackValid);
        Assert.False(mfc.Snapshot.IsOffline);
    }

    [Fact]
    public async Task SetFlowAsync_WritesNativeRampOnlyOnce()
    {
        var bus = new FakeFujikinMfcBus();
        var mfc = CreateMfc(bus);

        await mfc.InitializeAsync();
        await mfc.SetFlowAsync(100, TimeSpan.FromSeconds(5));

        Assert.Equal(1, bus.SetPointWriteCount);
        Assert.Equal(1, bus.RampTimeWriteCount);
        Assert.Equal(5_000, bus.LastRawRampTime);

        await mfc.ExecuteCycleAsync();
        await mfc.ExecuteCycleAsync();

        Assert.Equal(1, bus.SetPointWriteCount);
        Assert.Equal(1, bus.RampTimeWriteCount);
    }

    private static FujikinMfc CreateMfc(IFujikinMfcBus bus) =>
        new(
            new FujikinMfcDefinition
            {
                Mfc = new MfcDefinition
                {
                    Id = "MFC_SIH4",
                    ModuleId = "PM1",
                    DisplayName = "SiH4 MFC",
                    GasName = "SiH4",
                    FullScale = 500
                },
                Address = 0x21,
                RawFullScale = 10_000,
                RampTimeUnit = TimeSpan.FromMilliseconds(1)
            },
            bus);

    private sealed class FakeFujikinMfcBus : IFujikinMfcBus
    {
        public bool IsConnected { get; private set; }

        public short RawFeedback { get; init; }

        public short? LastRawSetPoint { get; private set; }

        public int SetPointWriteCount { get; private set; }

        public int RampTimeWriteCount { get; private set; }

        public int? LastRawRampTime { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task WriteSetPointAsync(
            byte address,
            short rawSetPoint,
            CancellationToken cancellationToken = default)
        {
            LastRawSetPoint = rawSetPoint;
            SetPointWriteCount++;
            return Task.CompletedTask;
        }

        public Task WriteRampTimeAsync(
            byte address,
            int rawRampTime,
            CancellationToken cancellationToken = default)
        {
            LastRawRampTime = rawRampTime;
            RampTimeWriteCount++;
            return Task.CompletedTask;
        }

        public Task<short> ReadIndicatedFlowAsync(
            byte address,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RawFeedback);
    }
}
