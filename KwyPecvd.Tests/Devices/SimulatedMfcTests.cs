using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KwyPecvd.Device;
using KwyPecvd.Device.Mfc;
using KwyPecvd.Device.Mfc.Simulated;

namespace KwyPecvd.Tests.Devices
{
    public sealed class SimulatedMfcTests
    {
        [Fact]
        public async Task SetFlowAsync_RejectsTargetOutsideFullScale()
        {
            var mfc = CreateMfc();
            var result = await mfc.SetFlowAsync(501, TimeSpan.Zero);

            Assert.False(result.Succeeded);
            Assert.Equal(ResultCodes.OutOfRange, result.Code);
        }

        [Fact]
        public async Task HoldAsync_StopsActiveRampAtCurrentValue()
        {
            var mfc = CreateMfc();
            await mfc.SetFlowAsync(100, TimeSpan.FromMilliseconds(200));
            await Task.Delay(60);
            await mfc.ExecuteCycleAsync();
            var beforeHold = mfc.Snapshot.SetPoint;

            var result = await mfc.HoldAsync();
            await Task.Delay(40);
            await mfc.ExecuteCycleAsync();

            Assert.True(result.Succeeded);
            Assert.False(mfc.Snapshot.IsRamping);
            Assert.InRange(mfc.Snapshot.SetPoint, beforeHold, beforeHold + 5);
        }

        [Fact]
        public async Task OutOfTolerance_IsRaisedOnlyAfterConfiguredDelay()
        {
            var mfc = CreateMfc(TimeSpan.FromMilliseconds(30));
            await mfc.SetFlowAsync(100, TimeSpan.Zero);

            await mfc.ExecuteCycleAsync();
            Assert.False(mfc.Snapshot.IsOutOfTolerance);

            await Task.Delay(40);
            await mfc.ExecuteCycleAsync();
            Assert.True(mfc.Snapshot.IsOutOfTolerance);

            for (var index = 0; index < 30; index++)
                await mfc.ExecuteCycleAsync();

            Assert.False(mfc.Snapshot.IsOutOfTolerance);
        }

        [Fact]
        public async Task SnapshotObserverException_DoesNotBreakHardwareCycle()
        {
            var mfc = CreateMfc();
            mfc.SnapshotChanged += (_, _) => throw new InvalidOperationException("Observer failed.");

            await mfc.SetFlowAsync(100, TimeSpan.Zero);
            var exception = await Record.ExceptionAsync(() => mfc.ExecuteCycleAsync());

            Assert.Null(exception);
            Assert.Equal(100, mfc.Snapshot.SetPoint);
        }

        [Fact]
        public async Task ShutdownAsync_DoesNotChangeProcessSetPoint()
        {
            var mfc = CreateMfc();
            await mfc.SetFlowAsync(100, TimeSpan.Zero);
            await mfc.ExecuteCycleAsync();

            await mfc.ShutdownAsync();
            await mfc.ExecuteCycleAsync();

            Assert.Equal(100, mfc.Snapshot.SetPoint);
        }

        [Fact]
        public void Definition_RejectsNonFiniteConfiguration()
        {
            var definition = CreateDefinition() with { FullScale = double.NaN };

            Assert.Throws<InvalidOperationException>(definition.Validate);
        }

        private static SimulatedMfc CreateMfc(TimeSpan? toleranceDelay = null)
        {
            var definition = CreateDefinition() with
            {
                ToleranceDelay = toleranceDelay ?? TimeSpan.FromSeconds(3)
            };

            return new SimulatedMfc(definition);
        }

        private static MfcDefinition CreateDefinition()
        {
            return new MfcDefinition
            {
                Id = "MFC_SIH4",
                ModuleId = "PM1",
                DisplayName = "SiH4 MFC",
                GasName = "SiH4",
                Unit = "sccm",
                FullScale = 500,
                Tolerance = 2,
                ToleranceDelay = TimeSpan.FromSeconds(3)
            };
        }
    }
}
