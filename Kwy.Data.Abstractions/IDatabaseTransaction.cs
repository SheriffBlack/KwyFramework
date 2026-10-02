using System.Data.Common;

namespace Kwy.Data.Abstractions;

/// <summary>
/// 表示一个已开启的数据库事务及其专用连接。
/// </summary>
/// <remarks>
/// 跨多条 SQL 的原子业务操作应使用同一实例，并通过
/// <see cref="ISqlExecutorFactory.Create(IDatabaseTransaction)"/> 创建事务绑定执行器。
/// 若未提交就释放，基础实现会尝试回滚。
/// </remarks>
public interface IDatabaseTransaction : IAsyncDisposable
{
    /// <summary>获取事务所属的打开连接。</summary>
    DbConnection Connection { get; }

    /// <summary>获取底层数据库事务。</summary>
    DbTransaction Transaction { get; }

    /// <summary>获取该事务绑定 SQL 执行器的默认超时秒数。</summary>
    int? CommandTimeoutSeconds { get; }

    /// <summary>异步提交事务，使业务变更正式生效。</summary>
    /// <param name="cancellationToken">取消提交操作的令牌。</param>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>异步回滚事务，撤销本事务中尚未提交的业务变更。</summary>
    /// <param name="cancellationToken">取消回滚操作的令牌。</param>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
