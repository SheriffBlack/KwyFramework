using System.Data.Common;
using System.Runtime.ExceptionServices;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

/// <summary>
/// 托管一个数据库事务及其专用连接的生命周期。
/// </summary>
/// <remarks>
/// 适用于多条写入必须全部成功或全部撤销的业务操作。
/// 正常结束时调用 <see cref="CommitAsync"/>；业务失败可显式调用 <see cref="RollbackAsync"/>。
/// 未完成就释放时会尝试回滚，且回滚失败也不会阻止事务和连接继续释放。
/// </remarks>
public sealed class DatabaseTransaction : IDatabaseTransaction
{
    private bool completed;
    private bool disposed;

    /// <summary>初始化事务生命周期容器。</summary>
    /// <param name="connection">已打开且由本实例接管的连接。</param>
    /// <param name="transaction">连接上已开启且由本实例接管的事务。</param>
    /// <param name="commandTimeoutSeconds">事务绑定 SQL 执行器继承的默认超时秒数。</param>
    public DatabaseTransaction(DbConnection connection, DbTransaction transaction, int? commandTimeoutSeconds = null)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        CommandTimeoutSeconds = commandTimeoutSeconds;
    }

    /// <inheritdoc />
    public DbConnection Connection { get; }

    /// <inheritdoc />
    public DbTransaction Transaction { get; }

    /// <inheritdoc />
    public int? CommandTimeoutSeconds { get; }

    /// <inheritdoc />
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    /// <inheritdoc />
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    /// <summary>
    /// 释放事务和连接；事务尚未完成时先尝试回滚。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Exception? failure = null;
        if (!completed)
        {
            try
            {
                await Transaction.RollbackAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        try
        {
            await Transaction.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }

        try
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
