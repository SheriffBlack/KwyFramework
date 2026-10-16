namespace KwyPecvd.Device.Mfc.Ramp;

/// <summary>将目标值和斜坡时间一次性交给 MFC 自身执行。</summary>
public sealed class NativeRampProfile : IMfcRampProfile
{
    private double target;
    private TimeSpan duration;
    private DateTimeOffset startedAt = DateTimeOffset.UtcNow;

    public MfcRampStep SetTarget(double target, TimeSpan duration, DateTimeOffset now)
    {
        this.target = target;
        this.duration = duration;
        startedAt = now;
        return CreateStep(now, requiresWrite: true);
    }

    public MfcRampStep Evaluate(DateTimeOffset now) =>
        CreateStep(now, requiresWrite: false);

    public MfcRampStep Hold(double feedback, DateTimeOffset now)
    {
        target = feedback;
        duration = TimeSpan.Zero;
        startedAt = now;
        return CreateStep(now, requiresWrite: true);
    }

    public void ConfirmWritten(double commandSetPoint)
    {
        // Native Ramp 在命令入口一次性写入，周期阶段不会再次发送。
    }

    private MfcRampStep CreateStep(DateTimeOffset now, bool requiresWrite) =>
        new(
            target,
            target,
            duration,
            requiresWrite,
            duration > TimeSpan.Zero && now - startedAt < duration);
}
