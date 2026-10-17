namespace KwyPecvd.Device.RfPowerSupply.Simulated;

/*
SetPowerAsync(500)
    ↓
基类立即调用 WritePowerSetPointAsync(500)
    ↓
EnableAsync()
    ↓
基类立即调用 WriteEnabledAsync(true)
    ↓
RT周期调用 ExecuteCycleAsync()
    ↓
基类调用 SimulatedRfPowerSupply.ReadFeedbackAsync()
    ↓
仿真正向功率逐步接近500 W
    ↓
基类判断功率偏差和反射功率
    ↓
更新 RfPowerSupplySnapshot
 
*/

/// <summary>
/// RF电源仿真实现。
/// 用于无真实硬件时验证功率控制、诊断和安全停机逻辑。
/// </summary>
public sealed class SimulatedRfPowerSupply: RfPowerSupplyBase
{
    private readonly object simulationSync = new();

    /// <summary>
    /// 每个周期向目标靠近的比例。
    /// 0.2 表示每周期消除20%的剩余偏差。
    /// </summary>
    private readonly double responseFactor;

    private double forwardPower;

    private double reflectedPowerRatio;

    private bool isOffline;

    private bool hasBadQuality;

    private bool isFaulted;
    private double commandedPower;
    private bool commandedEnabled;

    public SimulatedRfPowerSupply(
        RfPowerSupplyDefinition definition,
        double responseFactor = 0.2,
        double reflectedPowerRatio = 0.02)
        : base(definition)
    {
        if (!double.IsFinite(responseFactor) ||
            responseFactor <= 0 ||
            responseFactor > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(responseFactor));
        }

        if (!double.IsFinite(reflectedPowerRatio) ||
            reflectedPowerRatio < 0 ||
            reflectedPowerRatio > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reflectedPowerRatio));
        }

        this.responseFactor = responseFactor;
        this.reflectedPowerRatio =
            reflectedPowerRatio;
    }

    protected override ValueTask WritePowerSetPointAsync(
        double power,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (simulationSync)
        {
            commandedPower = power;
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask WriteEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (simulationSync)
        {
            if (enabled && isOffline)
                throw new IOException($"Simulated RF power supply '{Id}' is offline.");

            commandedEnabled = enabled;
        }

        return ValueTask.CompletedTask;
    }

    protected override ValueTask<RfPowerSupplyCycleResult> ReadFeedbackAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        lock (simulationSync)
        {
            var now = DateTimeOffset.UtcNow;

            var feedbackValid =
                !isOffline &&
                !hasBadQuality;

            var outputOn =
                commandedEnabled &&
                feedbackValid &&
                !isFaulted;

            var targetPower =
                outputOn
                    ? commandedPower
                    : 0;

            forwardPower +=
                (targetPower - forwardPower) *
                responseFactor;

            // 避免浮点数无限接近零，却始终不等于零。
            if (Math.Abs(forwardPower) < 0.001)
            {
                forwardPower = 0;
            }

            var reflectedPower =
                outputOn
                    ? forwardPower *
                      reflectedPowerRatio
                    : 0;

            return ValueTask.FromResult(
                new RfPowerSupplyCycleResult(
                    ForwardPower:
                        forwardPower,
                    ReflectedPower:
                        reflectedPower,
                    IsOutputOn:
                        outputOn,
                    IsOffline:
                        isOffline,
                    IsFeedbackValid:
                        feedbackValid,
                    IsFaulted:
                        isFaulted,
                    Timestamp:
                        now));
        }
    }

    /// <summary>
    /// 注入或解除通信离线状态。
    /// </summary>
    public void SetOffline(bool value)
    {
        lock (simulationSync)
        {
            isOffline = value;
        }
    }

    /// <summary>
    /// 注入或解除反馈质量异常。
    /// </summary>
    public void SetBadQuality(bool value)
    {
        lock (simulationSync)
        {
            hasBadQuality = value;
        }
    }

    /// <summary>
    /// 注入或解除RF电源自身故障。
    /// </summary>
    public void SetFaulted(bool value)
    {
        lock (simulationSync)
        {
            isFaulted = value;
        }
    }

    /// <summary>
    /// 调整反射功率比例，用于模拟匹配良好或失配。
    /// </summary>
    public void SetReflectedPowerRatio(
        double value)
    {
        if (!double.IsFinite(value) ||
            value < 0 ||
            value > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value));
        }

        lock (simulationSync)
        {
            reflectedPowerRatio = value;
        }
    }
}
