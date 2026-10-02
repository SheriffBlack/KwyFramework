namespace Kwy.Data.Abstractions;

/// <summary>
/// 定义 SQL 执行器的创建能力。
/// </summary>
/// <remarks>
/// Repository 可按数据源名称获取执行器；需要多条 SQL 原子执行时，
/// 必须传入已开启的 <see cref="IDatabaseTransaction"/> 创建绑定执行器。
/// </remarks>
public interface ISqlExecutorFactory
{
    /// <summary>创建绑定到指定数据源的无事务 SQL 执行器。</summary>
    /// <param name="dataSourceName">数据源逻辑名称。</param>
    /// <returns>每次操作自行打开和释放连接的执行器。</returns>
    ISqlExecutor Create(string dataSourceName = KwyDataSourceNames.Default);

    /// <summary>创建使用现有连接和事务的 SQL 执行器。</summary>
    /// <param name="transaction">已开启且尚未释放的事务。</param>
    /// <returns>生命周期不得超过传入事务的执行器。</returns>
    ISqlExecutor Create(IDatabaseTransaction transaction);
}
