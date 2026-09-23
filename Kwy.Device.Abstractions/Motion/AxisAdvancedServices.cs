namespace Kwy.Device.Abstractions.Motion;

/// <summary>提供标定误差表补偿；补偿表内容属于设备标定数据，不属于驱动器参数。</summary>
public interface IAxisErrorCompensationProvider
{
    double GetCompensation(string tableId, double engineeringPosition);
}

/// <summary>在工艺/标定坐标与机械坐标之间换算，业务不应直接叠加零点偏移。</summary>
public interface IAxisCoordinateTransformer
{
    double ToMachinePosition(AxisDefinition axis, double calibratedPosition);

    double ToCalibratedPosition(AxisDefinition axis, double machinePosition);
}

/// <summary>垂直轴抱闸的硬件输出能力；短轴号仅在物理卡适配层使用。</summary>
public interface IAxisBrake
{
    Task ReleaseAsync(short axis, CancellationToken cancellationToken = default);

    Task EngageAsync(short axis, CancellationToken cancellationToken = default);
}

/// <summary>协调伺服使能、抱闸延时与运动顺序，避免工艺代码自行控制抱闸 DO。</summary>
public interface IAxisBrakeCoordinator
{
    Task PrepareForMotionAsync(short axis, CancellationToken cancellationToken = default);

    Task CompleteMotionAsync(short axis, CancellationToken cancellationToken = default);

    Task DisableAxisAsync(short axis, CancellationToken cancellationToken = default);
}

/// <summary>物理卡坐标值，供底层算法使用；业务模型应使用 AxisDefinition.Id。</summary>
public sealed record AxisPositionCoordinate(short Axis, double Position);

/// <summary>多轴组合禁入区，例如机械手进入治具空间时 X、Y、Z 的联合限制。</summary>
public sealed record MultiAxisForbiddenZone(
    string Id,
    IReadOnlyDictionary<string, AxisForbiddenRange> Bounds)
{
    public bool Contains(IReadOnlyDictionary<string, double> positions)
        => Bounds.Count > 0 && Bounds.All(item =>
            positions.TryGetValue(item.Key, out double position) && item.Value.Contains(position));

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentNullException.ThrowIfNull(Bounds);
        if (Bounds.Count == 0)
            throw new ArgumentException("A multi-axis forbidden zone must contain at least one axis bound.", nameof(Bounds));
        foreach ((string axisId, AxisForbiddenRange range) in Bounds)
        {
            if (string.IsNullOrWhiteSpace(axisId)) throw new ArgumentException("Axis ID cannot be empty.", nameof(Bounds));
            range.Validate();
        }
    }
}

public interface IMultiAxisSafetyGuard
{
    /// <summary>以 AxisDefinition.Id 为键校验多轴目标位置。</summary>
    MotionAdmissionResult Validate(IReadOnlyDictionary<string, double> targetPositions);
}

/// <summary>为旋转轴选择合法目标角度，处理模周期、累计角度和缠绕禁区。</summary>
public interface IRotaryAxisPathPlanner
{
    double ResolveTarget(AxisDefinition axis, double currentPosition, double requestedPosition);
}
