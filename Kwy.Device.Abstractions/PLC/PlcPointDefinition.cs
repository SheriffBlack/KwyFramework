namespace Kwy.Device.Abstractions.PLC;

/// <summary>PLC 点位支持的标准数据类型。</summary>
public enum PlcDataType
{
    /// <summary>布尔值。</summary>
    Boolean,
    /// <summary>16 位有符号整数。</summary>
    Int16,
    /// <summary>16 位无符号整数。</summary>
    UInt16,
    /// <summary>32 位有符号整数。</summary>
    Int32,
    /// <summary>32 位无符号整数。</summary>
    UInt32,
    /// <summary>32 位单精度浮点数。</summary>
    Float,
    /// <summary>原始字节数据。</summary>
    Bytes
}

/// <summary>PLC 点位的访问权限。</summary>
public enum PlcPointAccess
{
    /// <summary>只允许读取。</summary>
    ReadOnly,
    /// <summary>允许读取和写入。</summary>
    ReadWrite,
    /// <summary>只允许写入。</summary>
    WriteOnly
}

/// <summary>PLC 点位的稳定业务身份及物理地址映射。</summary>
public sealed record PlcPointDefinition
{
    /// <summary>业务稳定 ID；流程、日志和配置引用均应使用此值。</summary>
    public required string Id { get; init; }
    /// <summary>面向用户的显示名称。</summary>
    public required string Name { get; init; }
    /// <summary>承载该点位的 PLC 设备 ID。</summary>
    public required string DeviceId { get; init; }
    /// <summary>厂商驱动可识别的 PLC 物理地址。</summary>
    public required string Address { get; init; }
    /// <summary>点位声明的数据类型。</summary>
    public required PlcDataType DataType { get; init; }
    /// <summary>点位访问权限。</summary>
    public PlcPointAccess Access { get; init; } = PlcPointAccess.ReadWrite;
    /// <summary>字节点位的读取长度；非字节点位必须为 1。</summary>
    public ushort Length { get; init; } = 1;
    /// <summary>可选工程单位。</summary>
    public string? Unit { get; init; }
    /// <summary>用于维护界面、诊断和筛选的可选分组。</summary>
    public string? Group { get; init; }
    /// <summary>面向维护人员的可选说明。</summary>
    public string? Description { get; init; }

    /// <summary>获取与点位数据类型对应的 CLR 类型。</summary>
    public Type ClrType => DataType switch
    {
        PlcDataType.Boolean => typeof(bool),
        PlcDataType.Int16 => typeof(short),
        PlcDataType.UInt16 => typeof(ushort),
        PlcDataType.Int32 => typeof(int),
        PlcDataType.UInt32 => typeof(uint),
        PlcDataType.Float => typeof(float),
        PlcDataType.Bytes => typeof(byte[]),
        _ => throw new ArgumentOutOfRangeException(nameof(DataType))
    };

    /// <summary>将支持的 CLR 类型转换为 PLC 标准数据类型。</summary>
    public static PlcDataType FromClrType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type == typeof(bool)) return PlcDataType.Boolean;
        if (type == typeof(short)) return PlcDataType.Int16;
        if (type == typeof(ushort)) return PlcDataType.UInt16;
        if (type == typeof(int)) return PlcDataType.Int32;
        if (type == typeof(uint)) return PlcDataType.UInt32;
        if (type == typeof(float)) return PlcDataType.Float;
        if (type == typeof(byte[])) return PlcDataType.Bytes;
        throw new NotSupportedException($"不支持的 PLC 点位 CLR 类型：{type.FullName}。");
    }

    /// <summary>校验点位自身的静态定义。</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Address);
        if (!Enum.IsDefined(DataType)) throw new ArgumentOutOfRangeException(nameof(DataType));
        if (!Enum.IsDefined(Access)) throw new ArgumentOutOfRangeException(nameof(Access));
        if (Length == 0) throw new ArgumentOutOfRangeException(nameof(Length));
        if (DataType != PlcDataType.Bytes && Length != 1)
            throw new InvalidOperationException($"PLC 点位“{Id}”仅在字节数据类型下可以指定 Length。");
    }
}
