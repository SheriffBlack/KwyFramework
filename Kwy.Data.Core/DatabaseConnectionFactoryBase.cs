using System.Data.Common;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

/// <summary>
/// 为具体数据库 Provider 提供连接工厂的通用基类。
/// </summary>
/// <remarks>
/// Provider 只需实现 <see cref="CreateConnection"/>。本基类负责验证数据源配置，
/// 并在打开连接失败时释放连接，避免业务持久化失败导致连接泄漏。
/// </remarks>
public abstract class DatabaseConnectionFactoryBase : IDatabaseConnectionFactory
{
    /// <summary>使用已验证的数据源配置初始化连接工厂。</summary>
    /// <param name="options">当前 Provider 对应的数据源配置。</param>
    protected DatabaseConnectionFactoryBase(KwyDataSourceOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Options.ValidateAndThrow();
    }

    /// <summary>获取供派生 Provider 创建连接使用的配置。</summary>
    protected KwyDataSourceOptions Options { get; }

    /// <inheritdoc />
    public string DataSourceName => Options.Name;

    /// <inheritdoc />
    public KwyDatabaseProvider Provider => Options.Provider;

    /// <inheritdoc />
    public int? CommandTimeoutSeconds => Options.CommandTimeoutSeconds;

    /// <inheritdoc />
    public abstract DbConnection CreateConnection();

    /// <inheritdoc />
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
