namespace Kwy.Device.Abstractions.IO;

/// <summary>IO 点位的信号方向。</summary>
public enum IoSignalKind
{
    /// <summary>物理数字输入，供逻辑状态读取。</summary>
    DigitalInput,
    /// <summary>物理数字输出，供逻辑状态写入。</summary>
    DigitalOutput
}

/// <summary>
/// 点位对工艺运行的重要程度。该分类用于诊断和软件策略，不能替代急停、门锁等硬件功能安全回路。
/// </summary>
public enum IoCriticality
{
    /// <summary>普通工艺信号。</summary>
    Standard,

    /// <summary>工艺关键点位；失效时通常需要阻止自动运行或进入受控处置。</summary>
    ProcessCritical,

    /// <summary>仅用于诊断和维护。</summary>
    DiagnosticOnly
}

/// <summary>
/// IO 点位的稳定逻辑身份与电气映射。
/// 工艺等待、超时和互锁规则不属于点位本身。
/// </summary>
public sealed record IoPointDefinition
{
    /// <summary>业务稳定 ID；流程、配方和事件均使用此值。</summary>
    public required string Id { get; init; }

    /// <summary>可本地化、可修改的显示名称。</summary>
    public required string Name { get; init; }
    /// <summary>承载该物理点位的 IIoCardDevice.DeviceId。</summary>
    public required string DeviceId { get; init; }
    /// <summary>点位方向；DI 和 DO 仍可由独立集合传入，但此字段用于配置自校验。</summary>
    public required IoSignalKind Kind { get; init; }
    /// <summary>设备内从零开始的物理通道号；上限由设备能力决定。</summary>
    public int Channel { get; init; }
    /// <summary>逻辑状态与物理电平是否相反；逻辑层统一处理。</summary>
    public bool Inverted { get; init; }

    /// <summary>
    /// 输出点在软件停机、报警处置等场景下应收敛到的工艺状态。
    /// null 表示不参与工艺输出收敛；这不是硬件功能安全状态，输入点不允许配置。
    /// </summary>
    public bool? ProcessSafeState { get; init; }

    /// <summary>工艺重要程度，用于流程准入和诊断，不能替代硬件安全回路。</summary>
    public IoCriticality Criticality { get; init; }
    /// <summary>用于 UI、维护和报警分组的可选名称。</summary>
    public string? Group { get; init; }
    /// <summary>允许写入该 DO 的资源所有者；null 表示不启用 Owner 限制。</summary>
    public string? Owner { get; init; }
    /// <summary>面向维护人员的可选说明。</summary>
    public string? Description { get; init; }

    /// <summary>校验点位自身的静态定义。</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        if (!Enum.IsDefined(Kind)) throw new ArgumentOutOfRangeException(nameof(Kind));
        if (!Enum.IsDefined(Criticality)) throw new ArgumentOutOfRangeException(nameof(Criticality));
        if (Channel < 0)
            throw new ArgumentOutOfRangeException(nameof(Channel), Channel, "Channel must be non-negative.");

        if (Kind == IoSignalKind.DigitalInput && ProcessSafeState is not null)
            throw new InvalidOperationException($"Input point '{Id}' cannot define ProcessSafeState.");
    }
}
