namespace Kwy.Device.PLC.Abstractions;

/// <summary>
/// PLC 点位定义的只读查询入口。
/// 与 IO 的 <c>IDigitalIoPointDefinitionProvider</c>、运动轴的 <c>IAxisDefinitionProvider</c> 对应；
/// 业务通过稳定 ID 查询定义，而不直接维护 PLC 地址字典。
/// </summary>
public interface IPlcPointDefinitionProvider
{
    /// <summary>获取全部 PLC 点位定义。</summary>
    IReadOnlyCollection<PlcPointDefinition> Definitions { get; }

    /// <summary>尝试按照稳定业务 ID 获取点位定义。</summary>
    bool TryGet(string pointId, out PlcPointDefinition point);

    /// <summary>按照稳定业务 ID 获取点位定义；不存在时抛出异常。</summary>
    PlcPointDefinition GetRequired(string pointId);
}

/// <summary>按照稳定业务点位 ID 读取 PLC 数据。</summary>
public interface ILogicalPlcReader
{
    /// <summary>读取点位值，并校验请求类型与点位声明的数据类型一致。</summary>
    Task<T> ReadAsync<T>(string pointId, CancellationToken cancellationToken = default);
}

/// <summary>按照稳定业务点位 ID 写入 PLC 数据。</summary>
public interface ILogicalPlcWriter
{
    /// <summary>写入点位值，并校验访问权限及数据类型。</summary>
    Task WriteAsync<T>(string pointId, T value, CancellationToken cancellationToken = default);
}

/// <summary>按照稳定业务点位 ID 读取换算后的 PLC 工程量。</summary>
public interface ILogicalPlcEngineeringReader
{
    /// <summary>读取数值点位的原始值，并根据点位量程转换为工程量。</summary>
    Task<double> ReadEngineeringAsync(string pointId, CancellationToken cancellationToken = default);
}

/// <summary>按照稳定业务点位 ID 写入 PLC 工程量。</summary>
public interface ILogicalPlcEngineeringWriter
{
    /// <summary>
    /// 将工程量换算为寄存器原始值并写入。
    /// 整数寄存器使用远离零的中点舍入。
    /// </summary>
    Task WriteEngineeringAsync(string pointId, double value, CancellationToken cancellationToken = default);
}
