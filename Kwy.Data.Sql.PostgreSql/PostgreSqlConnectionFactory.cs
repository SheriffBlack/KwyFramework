using System.Data.Common;
using Kwy.Data.Abstractions;
using Kwy.Data.Core;
using Npgsql;

namespace Kwy.Data.Sql.PostgreSql;

/// <summary>使用 Npgsql 为 PostgreSQL 数据源创建数据库连接。</summary>
public sealed class PostgreSqlConnectionFactory : DatabaseConnectionFactoryBase
{
    /// <summary>使用 PostgreSQL 数据源配置初始化连接工厂。</summary>
    public PostgreSqlConnectionFactory(KwyDataSourceOptions options)
        : base(options)
    {
        if (options.Provider != KwyDatabaseProvider.PostgreSql)
        {
            throw new ArgumentException("The data source provider must be PostgreSql.", nameof(options));
        }
    }

    /// <inheritdoc />
    public override DbConnection CreateConnection()
        => new NpgsqlConnection(Options.ConnectionString);
}
