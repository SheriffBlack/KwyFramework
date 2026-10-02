using System.Data;

namespace Kwy.Data.Abstractions;

/// <summary>
/// 不可变地描述一条待执行的 SQL 命令。
/// </summary>
/// <param name="Sql">SQL 文本或存储过程名称。</param>
/// <param name="Parameters">命令参数；业务值应通过参数传入，不应拼接进 SQL。</param>
/// <param name="CommandType">文本、存储过程等 ADO.NET 命令类型。</param>
/// <param name="TimeoutSeconds">本命令的超时秒数；为空时使用数据源或 ORM 默认值。</param>
/// <remarks>
/// 该类只描述技术命令，不承载工单、设备等业务语义。
/// 表名或列名等无法参数化的标识符必须来自受信任白名单。
/// </remarks>
public sealed record SqlCommandDefinition(
    string Sql,
    IReadOnlyList<SqlParameterValue>? Parameters = null,
    CommandType CommandType = CommandType.Text,
    int? TimeoutSeconds = null)
{
    /// <summary>创建文本类型的 SQL 命令定义。</summary>
    /// <param name="sql">SQL 文本。</param>
    /// <param name="parameters">按 Provider 命名规则定义的参数。</param>
    /// <returns>命令类型为 <see cref="System.Data.CommandType.Text"/> 的定义。</returns>
    public static SqlCommandDefinition Text(
        string sql,
        params SqlParameterValue[] parameters)
        => new(sql, parameters);
}
