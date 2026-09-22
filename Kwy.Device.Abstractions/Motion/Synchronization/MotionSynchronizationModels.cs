namespace Kwy.Device.Abstractions.Motion;

/// <summary>虚拟轴的执行位置；软件虚拟轴用于节拍/耦合主轴，控制器虚拟轴由厂商适配器实现。</summary>
public enum VirtualAxisHostKind
{
    /// <summary>软件虚拟轴（在上位机C#运动模块内运算）</summary>
    Software,

    /// <summary>控制器硬件虚拟轴（运动卡/TwinCAT实时内核运算）</summary>
    Controller
}

/// <summary>可作为插补、电子齿轮或电子凸轮主轴的逻辑虚拟轴。</summary>
public sealed record VirtualAxisDefinition : AxisResourceDefinition
{
    /// <summary>虚拟轴的位置计算由软件还是由控制器实时维护。</summary>
    public VirtualAxisHostKind HostKind { get; init; } = VirtualAxisHostKind.Software;
    /// <summary>仅 Controller 模式需要，表示承载虚拟轴的控制器设备。</summary>
    public string? HostDeviceId { get; init; }

    public void Validate()
    {
        ValidateCommon();
        if (!Enum.IsDefined(HostKind)) throw new ArgumentOutOfRangeException(nameof(HostKind));
        if (HostKind == VirtualAxisHostKind.Controller)
            ArgumentException.ThrowIfNullOrWhiteSpace(HostDeviceId);
        if (HostKind == VirtualAxisHostKind.Software && !string.IsNullOrWhiteSpace(HostDeviceId))
            throw new InvalidOperationException("Software virtual axes must not bind a controller device.");
    }
}

/// <summary>建立主从同步的时机，避免在任意主轴相位突然啮合造成机械冲击。</summary>
public enum SynchronizationEngageMode
{
    /// <summary>立即啮合</summary>
    Immediate,

    /// <summary>在主轴指定位置啮合</summary>
    AtMasterPosition
}

/// <summary>业务轴间的电子齿轮关系；比例为从轴单位 / 主轴单位。</summary>
public sealed record ElectronicGearDefinition
{
    /// <summary>稳定配置 ID，例如 coupling.g-to-z。</summary>
    public required string Id { get; init; }
    /// <summary>提供位置或速度基准的业务轴 ID，可为虚拟轴。</summary>
    public required string MasterAxisId { get; init; }
    /// <summary>随主轴按固定比例同步的业务物理轴 ID。</summary>
    public required string SlaveAxisId { get; init; }
    public double Ratio { get; init; } = 1;
    public double MasterOffset { get; init; }
    public double SlaveOffset { get; init; }
    public SynchronizationEngageMode EngageMode { get; init; } = SynchronizationEngageMode.Immediate;
    public double? EngageMasterPosition { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(MasterAxisId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SlaveAxisId);
        if (string.Equals(MasterAxisId, SlaveAxisId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Electronic gear master and slave must be different axes.");
        if (!double.IsFinite(Ratio) || Ratio == 0) throw new ArgumentOutOfRangeException(nameof(Ratio));
        if (!double.IsFinite(MasterOffset) || !double.IsFinite(SlaveOffset)) throw new ArgumentOutOfRangeException(nameof(MasterOffset));
        if (!Enum.IsDefined(EngageMode)) throw new ArgumentOutOfRangeException(nameof(EngageMode));
        if (EngageMode == SynchronizationEngageMode.AtMasterPosition && EngageMasterPosition is not { } position)
            throw new InvalidOperationException("An engagement position is required for AtMasterPosition mode.");
        if (EngageMasterPosition is { } value && !double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(EngageMasterPosition));
    }
}

/// <summary>业务轴间的电子凸轮关系；曲线内容由 CamProfileId 指向独立、可版本化的凸轮表。</summary>
public sealed record ElectronicCamDefinition
{
    /// <summary>稳定配置 ID，例如 transfer.pick-cam。</summary>
    public required string Id { get; init; }
    /// <summary>凸轮相位来源的业务轴 ID，可为虚拟轴。</summary>
    public required string MasterAxisId { get; init; }
    /// <summary>按凸轮曲线跟随的业务物理轴 ID。</summary>
    public required string FollowerAxisId { get; init; }
    /// <summary>凸轮曲线的可版本化配置标识，不直接把大量点表嵌入设备轴配置。</summary>
    public required string CamProfileId { get; init; }
    public double MasterOffset { get; init; }
    public double FollowerOffset { get; init; }
    public double Scale { get; init; } = 1;
    public SynchronizationEngageMode EngageMode { get; init; } = SynchronizationEngageMode.Immediate;
    public double? EngageMasterPosition { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(MasterAxisId);
        ArgumentException.ThrowIfNullOrWhiteSpace(FollowerAxisId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CamProfileId);
        if (string.Equals(MasterAxisId, FollowerAxisId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Electronic cam master and follower must be different axes.");
        if (!double.IsFinite(MasterOffset) || !double.IsFinite(FollowerOffset) || !double.IsFinite(Scale) || Scale == 0)
            throw new ArgumentOutOfRangeException(nameof(Scale));
        if (!Enum.IsDefined(EngageMode)) throw new ArgumentOutOfRangeException(nameof(EngageMode));
        if (EngageMode == SynchronizationEngageMode.AtMasterPosition && EngageMasterPosition is not { } position)
            throw new InvalidOperationException("An engagement position is required for AtMasterPosition mode.");
        if (EngageMasterPosition is { } value && !double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(EngageMasterPosition));
    }
}

/// <summary>提供统一业务轴资源中的虚拟轴定义。</summary>
public interface IVirtualAxisDefinitionProvider
{
    IReadOnlyCollection<VirtualAxisDefinition> VirtualAxes { get; }

    VirtualAxisDefinition GetVirtualAxis(string axisId);
}

/// <summary>提供设备配置中的电子齿轮与电子凸轮关系。</summary>
public interface IMotionSynchronizationDefinitionProvider
{
    IReadOnlyCollection<ElectronicGearDefinition> ElectronicGears { get; }
    IReadOnlyCollection<ElectronicCamDefinition> ElectronicCams { get; }

    ElectronicGearDefinition GetElectronicGear(string id);

    ElectronicCamDefinition GetElectronicCam(string id);
}