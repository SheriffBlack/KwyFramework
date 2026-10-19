using KwyPecvd.Device.Mfc.Ramp;

namespace KwyPecvd.Tests.Devices;

public sealed class MfcRampProfileTests
{
    [Fact]
    public void SoftwareRamp_WritesFinalValueOnlyOnce()
    {
        var profile = new SoftwareRampProfile();
        var startedAt = DateTimeOffset.UtcNow;

        profile.SetTarget(100, TimeSpan.FromSeconds(1), startedAt);

        MfcRampStep middle = profile.Evaluate(startedAt.AddMilliseconds(500));
        Assert.True(middle.RequiresWrite);
        Assert.Equal(50, middle.CommandSetPoint, 3);
        profile.ConfirmWritten(middle.CommandSetPoint);

        MfcRampStep final = profile.Evaluate(startedAt.AddSeconds(1));
        Assert.True(final.RequiresWrite);
        Assert.Equal(100, final.CommandSetPoint);
        profile.ConfirmWritten(final.CommandSetPoint);

        MfcRampStep stable = profile.Evaluate(startedAt.AddSeconds(2));
        Assert.False(stable.RequiresWrite);
        Assert.Equal(100, stable.CommandSetPoint);
    }

    [Fact]
    public void SoftwareRamp_RetriesValueUntilWriteIsConfirmed()
    {
        var profile = new SoftwareRampProfile();
        var now = DateTimeOffset.UtcNow;
        profile.SetTarget(100, TimeSpan.Zero, now);

        MfcRampStep firstAttempt = profile.Evaluate(now);
        MfcRampStep retry = profile.Evaluate(now.AddMilliseconds(100));

        Assert.True(firstAttempt.RequiresWrite);
        Assert.True(retry.RequiresWrite);

        profile.ConfirmWritten(retry.CommandSetPoint);

        Assert.False(
            profile.Evaluate(now.AddMilliseconds(200)).RequiresWrite);
    }
}
