using System.Data;
using Kwy.Data.Abstractions;
using Kwy.Data.EFCore;
using Kwy.Data.EFCore.Sqlite;
using Kwy.Data.Sql;
using Kwy.Data.Sql.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kwy.Data.Tests;

public sealed class DataLayerTests
{
    [Fact]
    public async Task TransactionBoundExecutor_RollsBackCommands()
    {
        const string connectionString = "Data Source=transaction-tests;Mode=Memory;Cache=Shared";
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();

        await using ServiceProvider provider = new ServiceCollection()
            .AddKwySqlite(connectionString)
            .BuildServiceProvider();

        ISqlExecutor sql = provider.GetRequiredService<ISqlExecutor>();
        await sql.ExecuteAsync(SqlCommandDefinition.Text("create table Samples (Id integer primary key, Value text not null)"));

        IDatabaseTransactionFactory transactions = provider.GetRequiredService<IDatabaseTransactionFactory>();
        ISqlExecutorFactory executors = provider.GetRequiredService<ISqlExecutorFactory>();
        await using (IDatabaseTransaction transaction = await transactions.BeginTransactionAsync())
        {
            ISqlExecutor transactionSql = executors.Create(transaction);
            await transactionSql.ExecuteAsync(SqlCommandDefinition.Text("insert into Samples (Value) values ('pending')"));
            await transaction.RollbackAsync();
        }

        long count = await sql.ExecuteScalarAsync<long>(SqlCommandDefinition.Text("select count(*) from Samples"));
        Assert.Equal(0, count);
    }

    [Fact]
    public void NamedDataSources_AreResolvedIndependently()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKwySqlite("Data Source=primary.db", "Primary")
            .AddKwySqlite("Data Source=history.db", "History")
            .BuildServiceProvider();

        IDatabaseConnectionFactoryResolver resolver = provider.GetRequiredService<IDatabaseConnectionFactoryResolver>();

        Assert.Equal("Primary", resolver.GetRequired("Primary").DataSourceName);
        Assert.Equal("History", resolver.GetRequired("History").DataSourceName);
        Assert.NotSame(resolver.GetRequired("Primary"), resolver.GetRequired("History"));
    }

    [Fact]
    public async Task EfCoreBridge_RestoresInitiallyClosedConnection()
    {
        await using ServiceProvider provider = new ServiceCollection()
            .AddKwyEfCoreSqlite<TestDbContext>("Data Source=:memory:")
            .BuildServiceProvider();

        await using TestDbContext context = await provider
            .GetRequiredService<IDbContextFactory<TestDbContext>>()
            .CreateDbContextAsync();

        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
        ISqlExecutor executor = provider.GetRequiredService<IEfCoreSqlBridge<TestDbContext>>().CreateExecutor(context);

        int value = await executor.ExecuteScalarAsync<int>(SqlCommandDefinition.Text("select 1"));

        Assert.Equal(1, value);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Pagination_UsesLongWithoutOverflow()
    {
        var page = new PageRequest(int.MaxValue, int.MaxValue);
        var result = new PagedResult<int>([], long.MaxValue, new PageRequest(0, 1));

        Assert.Equal((long)int.MaxValue * int.MaxValue, page.Offset);
        Assert.Equal(long.MaxValue, result.TotalPages);
    }

    [Fact]
    public async Task SqlExecutor_UsesDataSourceTimeoutByDefault()
    {
        var options = new KwyDataSourceOptions
        {
            Provider = KwyDatabaseProvider.Sqlite,
            ConnectionString = "Data Source=:memory:",
            CommandTimeoutSeconds = 17
        };
        var executor = new InspectingSqlExecutor(new SqliteConnectionFactory(options));

        await executor.ExecuteScalarAsync<int>(SqlCommandDefinition.Text("select 1"));

        Assert.Equal(17, executor.LastCommandTimeout);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    private sealed class InspectingSqlExecutor(IDatabaseConnectionFactory connectionFactory)
        : DbCommandSqlExecutor(connectionFactory)
    {
        public int? LastCommandTimeout { get; private set; }

        protected override System.Data.Common.DbCommand CreateCommand(
            System.Data.Common.DbConnection connection,
            SqlCommandDefinition definition)
        {
            System.Data.Common.DbCommand command = base.CreateCommand(connection, definition);
            LastCommandTimeout = command.CommandTimeout;
            return command;
        }
    }
}
