using System.Data.Common;

namespace Kwy.Data.Abstractions;

/// <summary>
/// 定义某个逻辑数据源的数据库连接创建能力。
/// </summary>
/// <remarks>
/// Provider 实现该接口，业务 Repository 通常应使用 <see cref="ISqlExecutor"/>
/// 或 ORM，仅在需要 Provider 原生能力时直接获取连接。
/// 由该工厂返回的连接归调用方所有，必须及时释放。
/// </remarks>
public interface IDatabaseConnectionFactory
{
    /// <summary>获取数据源的逻辑名称，例如 Default、History 或 Mes。</summary>
    string DataSourceName { get; }

    /// <summary>获取数据源使用的数据库类型。</summary>
    KwyDatabaseProvider Provider { get; }

    /// <summary>获取数据源级别的默认命令超时秒数。</summary>
    int? CommandTimeoutSeconds { get; }

    /// <summary>
    /// 创建一个尚未打开的数据库连接。
    /// </summary>
    /// <returns>由调用方负责打开和释放的连接。</returns>
    DbConnection CreateConnection();

    /// <summary>
    /// 创建并打开数据库连接。
    /// </summary>
    /// <param name="cancellationToken">取消打开连接操作的令牌。</param>
    /// <returns>已打开且由调用方负责释放的连接。</returns>
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
