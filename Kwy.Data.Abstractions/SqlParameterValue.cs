using System.Data;

namespace Kwy.Data.Abstractions;

/// <summary>
/// 描述一个 Provider 无关的 SQL 参数。
/// </summary>
/// <remarks>
/// 输入参数用于安全传递业务值；输出或返回参数执行后，
/// <see cref="Value"/> 会被更新为数据库返回值。
/// </remarks>
public sealed record SqlParameterValue
{
    /// <summary>创建 SQL 参数定义。</summary>
    /// <param name="Name">参数名称，前缀遵循具体 Provider 规则。</param>
    /// <param name="Value">输入值；<see langword="null"/> 会转换为数据库 NULL。</param>
    /// <param name="DbType">可选的通用数据库类型。</param>
    /// <param name="Direction">输入、输出、输入输出或返回值方向。</param>
    /// <param name="Size">字符串或二进制参数的最大长度。</param>
    /// <param name="Precision">精确数值的总有效位数。</param>
    /// <param name="Scale">精确数值的小数位数。</param>
    public SqlParameterValue(
        string Name,
        object? Value,
        DbType? DbType = null,
        ParameterDirection Direction = ParameterDirection.Input,
        int? Size = null,
        byte? Precision = null,
        byte? Scale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        this.Name = Name;
        this.Value = Value;
        this.DbType = DbType;
        this.Direction = Direction;
        this.Size = Size;
        this.Precision = Precision;
        this.Scale = Scale;
    }

    /// <summary>获取参数名称。</summary>
    public string Name { get; }

    /// <summary>获取或设置输入值，或读取执行后回写的输出/返回值。</summary>
    public object? Value { get; set; }

    /// <summary>获取通用数据库类型。</summary>
    public DbType? DbType { get; }

    /// <summary>获取参数方向。</summary>
    public ParameterDirection Direction { get; }

    /// <summary>获取参数最大长度。</summary>
    public int? Size { get; }

    /// <summary>获取精确数值的总有效位数。</summary>
    public byte? Precision { get; }

    /// <summary>获取精确数值的小数位数。</summary>
    public byte? Scale { get; }
}
