using Kwy.Data.Abstractions;

namespace Kwy.Data.Sql;

public sealed class SqlExecutorFactory : ISqlExecutorFactory
{
    private readonly IDatabaseConnectionFactoryResolver connectionFactoryResolver;

    public SqlExecutorFactory(IDatabaseConnectionFactoryResolver connectionFactoryResolver)
    {
        this.connectionFactoryResolver = connectionFactoryResolver ?? throw new ArgumentNullException(nameof(connectionFactoryResolver));
    }

    public ISqlExecutor Create(string dataSourceName = KwyDataSourceNames.Default)
        => new DbCommandSqlExecutor(connectionFactoryResolver.GetRequired(dataSourceName));

    public ISqlExecutor Create(IDatabaseTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return new DbCommandSqlExecutor(transaction);
    }
}
