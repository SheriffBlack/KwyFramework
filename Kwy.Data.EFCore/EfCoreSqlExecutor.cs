using System.Data;
using System.Data.Common;
using Kwy.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kwy.Data.EFCore;

public sealed class EfCoreSqlExecutor : ISqlExecutor
{
    private readonly DbContext dbContext;

    public EfCoreSqlExecutor(DbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<int> ExecuteAsync(SqlCommandDefinition command, CancellationToken cancellationToken = default)
    {
        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        return await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> ExecuteScalarAsync<T>(SqlCommandDefinition command, CancellationToken cancellationToken = default)
    {
        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        var value = await dbCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ConvertValue<T>(value);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);

        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(map(reader));
        }

        return results;
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);

        await using var dbCommand = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return default;
        }

        var result = map(reader);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The query returned more than one row.");
        }

        return result;
    }

    private async ValueTask<DbCommand> CreateCommandAsync(
        SqlCommandDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Sql);

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandType = definition.CommandType;
        if (definition.TimeoutSeconds.HasValue)
        {
            command.CommandTimeout = definition.TimeoutSeconds.Value;
        }

        var currentTransaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (currentTransaction != null)
        {
            command.Transaction = currentTransaction;
        }

        AddParameters(command, definition.Parameters);
        return command;
    }

    private static void AddParameters(DbCommand command, IReadOnlyList<SqlParameterValue>? parameters)
    {
        if (parameters == null)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            dbParameter.Direction = parameter.Direction;
            if (parameter.DbType.HasValue)
            {
                dbParameter.DbType = parameter.DbType.Value;
            }

            command.Parameters.Add(dbParameter);
        }
    }

    private static T? ConvertValue<T>(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return default;
        }

        if (value is T typed)
        {
            return typed;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(value, targetType);
    }
}
