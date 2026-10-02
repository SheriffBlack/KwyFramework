using System.Data.Common;

namespace Kwy.Data.Abstractions;

/// <summary>
/// 定义不带 ORM 实体跟踪的最小 SQL 执行能力。
/// </summary>
/// <remarks>
/// 业务 Repository 负责编写 SQL 和映射业务对象；执行器只负责命令、参数、
/// 连接与 Reader 的生命周期。跨多条 SQL 的原子业务操作应使用事务绑定执行器。
/// </remarks>
public interface ISqlExecutor
{
    /// <summary>
    /// 执行不需要返回结果集的 SQL，返回受影响行数。
    /// </summary>
    /// <remarks>
    /// 适用于：
    ///     - INSERT
    ///     - UPDATE
    ///     - DELETE
    ///     - DDL
    ///     - 不返回结果集的存储过程
    /// </remarks>
    /// <param name="command">要执行的 SQL 命令定义。</param>
    /// <param name="cancellationToken">取消数据库操作的令牌。</param>
    /// <returns>数据库报告的受影响行数。</returns>
    Task<int> ExecuteAsync(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取结果集第一行第一列的单个值。
    /// </summary>
    /// <remarks>
    /// 适用于：
    ///     - COUNT
    ///     - SUM
    ///     - MAX
    ///     - 新增记录返回的 ID
    ///     - 单个状态或配置值
    /// 无结果或数据库返回 NULL 时返回 default(T)。
    /// </remarks>
    /// <typeparam name="T">期望返回的业务标量类型。</typeparam>
    /// <param name="command">要执行的 SQL 命令定义。</param>
    /// <param name="cancellationToken">取消数据库操作的令牌。</param>
    /// <returns>第一行第一列转换后的值；无值或数据库 NULL 时返回 <see langword="default"/>。</returns>
    Task<T?> ExecuteScalarAsync<T>(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取多行结果，并通过映射函数将每行转换成 <typeparamref name="T"/>。
    /// </summary>
    /// <remarks>
    /// 适用于：
    ///     - 列表查询
    ///     - 历史曲线点查询
    ///     - 报表数据
    ///     - 分页数据明细
    /// 返回值始终是集合；没有数据时返回空集合。
    /// </remarks>
    /// <typeparam name="T">每行映射为的业务类型。</typeparam>
    /// <param name="command">要执行的查询命令。</param>
    /// <param name="map">将当前 Reader 行转换为业务对象的映射函数。</param>
    /// <param name="cancellationToken">取消数据库操作的令牌。</param>
    /// <returns>映射后的只读集合；无数据时返回空集合。</returns>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询零行或一行数据。
    /// </summary>
    /// <remarks>
    /// 适用于：
    ///     主键、唯一编码等本应最多返回一行的查询。多于一行时抛异常可以及时暴露唯一性约束或 SQL 条件错误。
    /// </remarks>
    /// <typeparam name="T">查询行映射为的业务类型。</typeparam>
    /// <param name="command">应最多返回一行的查询命令。</param>
    /// <param name="map">将当前 Reader 行转换为业务对象的映射函数。</param>
    /// <param name="cancellationToken">取消数据库操作的令牌。</param>
    /// <returns>唯一结果；无数据时返回 <see langword="default"/>。</returns>
    /// <exception cref="InvalidOperationException">查询返回多于一行。</exception>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default);
}
