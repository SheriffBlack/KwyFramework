namespace KwyPecvd.Device.Thermal.TemperatureController;

public sealed record TemperatureControllerSnapshot
{
    public required string Id { get; init; }

    /// <summary>当前命令目标温度。</summary>
    public required double SetPoint { get; init; }

    /// <summary>控制器的实际温度反馈。</summary>
    public required double Feedback { get; init; }

    public required string Unit { get; init; }

    /// <summary>软件是否要求温控器使能。</summary>
    public required bool CommandedEnabled { get; init; }

    /// <summary>硬件反馈是否正在输出加热功率。</summary>
    public required bool IsPowerOn { get; init; }

    public required bool IsOffline { get; init; }

    public required bool IsFeedbackValid { get; init; }

    public required bool IsOutOfTolerance { get; init; }

    public required string? DiagnosticCode { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}