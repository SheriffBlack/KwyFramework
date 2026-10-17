namespace KwyPecvd.Device.Mfc.Ramp;

/// <summary>由 RT 周期计算中间设定值并逐周期写入硬件。</summary>
public sealed class SoftwareRampProfile : IMfcRampProfile
{
    private double start;
    private double target;
    private TimeSpan duration;
    private DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private double? lastWrittenCommand;

    public MfcRampStep SetTarget(double target, TimeSpan duration, DateTimeOffset now)
    {
        start = CalculateCommand(now);
        this.target = target;
        this.duration = duration;
        startedAt = now;
        return CreateStep(now, allowWrite: false);
    }

    public MfcRampStep Evaluate(DateTimeOffset now) => CreateStep(now, allowWrite: true);

    public MfcRampStep Hold(double feedback, DateTimeOffset now)
    {
        double command = CalculateCommand(now);
        start = command;
        target = command;
        duration = TimeSpan.Zero;
        startedAt = now;
        return CreateStep(now, allowWrite: false);
    }

    public void ConfirmWritten(double commandSetPoint)
    {
        lastWrittenCommand = commandSetPoint;
    }

    private MfcRampStep CreateStep(DateTimeOffset now, bool allowWrite)
    {
        double command = CalculateCommand(now);
        bool requiresWrite =
            allowWrite &&
            (!lastWrittenCommand.HasValue ||
             Math.Abs(command - lastWrittenCommand.Value) > 0.001);

        return new MfcRampStep(
            target,
            command,
            duration,
            requiresWrite,
            Math.Abs(command - target) > 0.001);
    }

    private double CalculateCommand(DateTimeOffset now)
    {
        if (duration <= TimeSpan.Zero)
            return target;

        TimeSpan elapsed = now - startedAt;
        if (elapsed >= duration)
            return target;

        double progress = Math.Clamp(
            elapsed.TotalMilliseconds / duration.TotalMilliseconds,
            0,
            1);
        return start + (target - start) * progress;
    }
}
