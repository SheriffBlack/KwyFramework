namespace KwyPecvd.Device.Thermal.TemperatureController.Io;

/*
SetPointAoId       → AO，目标温度
FeedbackAiId       → AI，实际控制温度
EnableDoId         → DO，加热允许
PowerOnDiId        → DI，实际功率输出反馈，可选
SensorFaultDiId    → DI，控制热偶断线，可选
OfflineDiId        → DI，控制器离线，可选
*/

/// <summary>
/// 基于模拟量和数字量IO的温度控制器接线定义。
/// </summary>
public sealed record IoTemperatureControllerDefinition
{
    public required TemperatureControllerDefinition Controller { get; init; }

    /// <summary>目标温度模拟量输出。</summary>
    public required string SetPointAoId { get; init; }

    /// <summary>控制温度模拟量输入。</summary>
    public required string FeedbackAiId { get; init; }

    /// <summary>加热允许数字量输出。</summary>
    public required string EnableDoId { get; init; }

    /// <summary>实际加热功率输出反馈。</summary>
    public string? PowerOnDiId { get; init; }

    /// <summary>控制温度传感器故障反馈。</summary>
    public string? SensorFaultDiId { get; init; }

    /// <summary>温控器离线反馈。</summary>
    public string? OfflineDiId { get; init; }

    /// <summary>
    /// 没有功率反馈DI时，是否允许使用使能命令
    /// 作为功率状态的替代值。
    /// </summary>
    public bool UseEnableCommandAsPowerFeedback
    {
        get;
        init;
    }

    /// <summary>写入AO和DO时使用的资源所有者。</summary>
    public string? OutputOwner { get; init; }

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(
            Controller);

        Controller.Validate();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            SetPointAoId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            FeedbackAiId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            EnableDoId);

        ValidateOptionalId(
            PowerOnDiId,
            nameof(PowerOnDiId));

        ValidateOptionalId(
            SensorFaultDiId,
            nameof(SensorFaultDiId));

        ValidateOptionalId(
            OfflineDiId,
            nameof(OfflineDiId));

        if (PowerOnDiId is null &&
            !UseEnableCommandAsPowerFeedback)
        {
            throw new InvalidOperationException(
                $"Temperature controller " +
                $"'{Controller.Id}' must define " +
                "a power feedback DI, or explicitly " +
                "enable command feedback.");
        }
    }

    private static void ValidateOptionalId(
        string? value,
        string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                value,
                parameterName);
        }
    }
}