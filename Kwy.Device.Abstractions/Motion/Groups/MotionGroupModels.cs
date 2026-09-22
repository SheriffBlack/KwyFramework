namespace Kwy.Device.Abstractions.Motion;

/// <summary>同一物理卡上可插补的一组业务轴。</summary>
public sealed record MotionGroupDefinition
{
    /// <summary>稳定业务 ID，例如 transport.xy。</summary>
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>成员轴必须全部属于此物理卡。</summary>
    public required string DeviceId { get; init; }
    /// <summary>定义物理插补轴顺序的业务轴 ID 列表。</summary>
    public required IReadOnlyList<string> AxisIds { get; init; }
    /// <summary>控制器内坐标系编号，仅由设备配置维护。</summary>
    public short CoordinateSystemChannel { get; init; }
    public MotionProfile DefaultProfile { get; init; } = new(100, 500, 500);
    public double DefaultTolerance { get; init; } = 0.01;
    public IReadOnlyList<MultiAxisForbiddenZone> ForbiddenZones { get; init; } = Array.Empty<MultiAxisForbiddenZone>();

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        ArgumentNullException.ThrowIfNull(AxisIds);
        if (AxisIds.Count < 2 || AxisIds.Any(string.IsNullOrWhiteSpace) || AxisIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != AxisIds.Count)
            throw new ArgumentException("A motion group requires at least two unique axis IDs.", nameof(AxisIds));
        if (CoordinateSystemChannel < 0) throw new ArgumentOutOfRangeException(nameof(CoordinateSystemChannel));
        if (!double.IsFinite(DefaultTolerance) || DefaultTolerance < 0) throw new ArgumentOutOfRangeException(nameof(DefaultTolerance));
        ArgumentNullException.ThrowIfNull(DefaultProfile);
        foreach (MultiAxisForbiddenZone zone in ForbiddenZones) zone.Validate();
    }
}

/// <summary>业务轴目标位置集合；键必须为运动组成员 AxisDefinition.Id。</summary>
public sealed record LinearMoveCommand(
    string GroupId,
    IReadOnlyDictionary<string, double> TargetPositions,
    MotionProfile? Profile = null,
    double? Tolerance = null,
    TimeSpan? Timeout = null);

/// <summary>圆弧插补的旋转方向，用于笛卡尔圆弧运动。</summary>
public enum ArcDirection
{
    /// <summary>CW 顺时针</summary>
    Clockwise,

    /// <summary>CCW 逆时针</summary>
    CounterClockwise
}

/// <summary>圆弧插补命令；目标和圆心均以业务轴 ID 表达。</summary>
public sealed record ArcMoveCommand(
    string GroupId,
    IReadOnlyDictionary<string, double> TargetPositions,
    IReadOnlyDictionary<string, double> CenterPositions,
    ArcDirection Direction,
    MotionProfile? Profile = null,
    double? Tolerance = null,
    TimeSpan? Timeout = null);