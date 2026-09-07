using System.Data.Common;
using Kwy.Data.Abstractions;
using Kwy.Data.Core;
using Microsoft.Data.Sqlite;

namespace Kwy.Data.Sql.Sqlite;

public sealed class SqliteConnectionFactory : DatabaseConnectionFactoryBase
{
    public SqliteConnectionFactory(KwyDataSourceOptions options)
        : base(options)
    {
    }

    public override DbConnection CreateConnection()
        => new SqliteConnection(Options.ConnectionString);
}
