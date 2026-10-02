using System.Data;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

/// <summary>
/// 基于命名连接工厂创建数据库事务。
/// </summary>
/// <remarks>
/// 该工厂用于工单主表与明细、历史批次与采样点等必须原子写入的业务场景。
/// 连接或事务创建失败时会释放已创建的连接。
/// </remarks>
public sealed class DatabaseTransactionFactory : IDatabaseTransactionFactory
{
    private readonly IDatabaseConnectionFactoryResolver connectionFactoryResolver;

    /// <summary>使用数据源解析器初始化事务工厂。</summary>
    /// <param name="connectionFactoryResolver">用于按名称定位事务数据源的解析器。</param>
    public DatabaseTransactionFactory(IDatabaseConnectionFactoryResolver connectionFactoryResolver)
    {
        this.connectionFactoryResolver = connectionFactoryResolver ?? throw new ArgumentNullException(nameof(connectionFactoryResolver));
    }

    /// <inheritdoc />
    public async ValueTask<IDatabaseTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        return await BeginTransactionAsync(KwyDataSourceNames.Default, isolationLevel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IDatabaseTransaction> BeginTransactionAsync(
        string dataSourceName,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        IDatabaseConnectionFactory connectionFactory = connectionFactoryResolver.GetRequired(dataSourceName);
        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            return new DatabaseTransaction(connection, transaction, connectionFactory.CommandTimeoutSeconds);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
