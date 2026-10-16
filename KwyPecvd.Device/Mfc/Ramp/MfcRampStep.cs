namespace KwyPecvd.Device.Mfc.Ramp;

/// <summary>某一时刻的 MFC 斜坡决策。</summary>
public readonly record struct MfcRampStep(
    double TargetSetPoint,
    double CommandSetPoint,
    TimeSpan RampDuration,
    bool RequiresWrite,
    bool IsRamping);
