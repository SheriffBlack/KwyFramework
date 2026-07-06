using System.Data.Common;

namespace Kwy.Data.Abstractions;

public interface IDatabaseConnectionFactory
{
    string DataSourceName { get; }

    KwyDatabaseProvider Provider { get; }

    DbConnection CreateConnection();

    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
