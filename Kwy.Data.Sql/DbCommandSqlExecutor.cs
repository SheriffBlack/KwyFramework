using System.Data.Common;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Sql;

public class DbCommandSqlExecutor : ISqlExecutor
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public DbCommandSqlExecutor(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<int> ExecuteAsync(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = CreateCommand(connection, command);
        return await dbCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = CreateCommand(connection, command);
        var value = await dbCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ConvertValue<T>(value);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = CreateCommand(connection, command);
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

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var dbCommand = CreateCommand(connection, command);
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

    protected virtual DbCommand CreateCommand(DbConnection connection, SqlCommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Sql);

        var command = connection.CreateCommand();
        command.CommandText = definition.Sql;
        command.CommandType = definition.CommandType;
        if (definition.TimeoutSeconds.HasValue)
        {
            command.CommandTimeout = definition.TimeoutSeconds.Value;
        }

        AddParameters(command, definition.Parameters);
        return command;
    }

    protected static void AddParameters(DbCommand command, IReadOnlyList<SqlParameterValue>? parameters)
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

    protected static T? ConvertValue<T>(object? value)
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
