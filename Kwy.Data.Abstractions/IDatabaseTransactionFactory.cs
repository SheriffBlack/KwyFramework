using System.Data;

namespace Kwy.Data.Abstractions;

/// <summary>
/// 定义为默认或指定数据源开启事务的能力。
/// </summary>
public interface IDatabaseTransactionFactory
{
    /// <summary>在默认数据源上开启事务。</summary>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <param name="cancellationToken">取消打开连接或开启事务的令牌。</param>
    /// <returns>已开启的事务，调用方必须异步释放。</returns>
    ValueTask<IDatabaseTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>在指定命名数据源上开启事务。</summary>
    /// <param name="dataSourceName">数据源逻辑名称，例如 History 或 Mes。</param>
    /// <param name="isolationLevel">事务隔离级别。</param>
    /// <param name="cancellationToken">取消打开连接或开启事务的令牌。</param>
    /// <returns>已开启的事务，调用方必须异步释放。</returns>
    ValueTask<IDatabaseTransaction> BeginTransactionAsync(
        string dataSourceName,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
