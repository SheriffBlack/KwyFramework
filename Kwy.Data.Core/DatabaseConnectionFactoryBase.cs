using System.Data.Common;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

public abstract class DatabaseConnectionFactoryBase : IDatabaseConnectionFactory
{
    protected DatabaseConnectionFactoryBase(KwyDataSourceOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Options.ValidateAndThrow();
    }

    protected KwyDataSourceOptions Options { get; }

    public string DataSourceName => Options.Name;

    public KwyDatabaseProvider Provider => Options.Provider;

    public abstract DbConnection CreateConnection();

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
