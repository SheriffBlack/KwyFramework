using System.Data.Common;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Sql;

public class DbCommandSqlExecutor : ISqlExecutor
{
    private readonly IDatabaseConnectionFactory connectionFactory;
    private readonly IDatabaseTransaction? transaction;

    public DbCommandSqlExecutor(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    internal DbCommandSqlExecutor(IDatabaseTransaction transaction)
    {
        this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        connectionFactory = null!;
    }

    public async Task<int> ExecuteAsync(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await CreateCommandLeaseAsync(command, cancellationToken).ConfigureAwait(false);
        int affectedRows = await lease.Command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        CopyOutputParameterValues(lease.Command, command.Parameters);
        return affectedRows;
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default)
    {
        await using var lease = await CreateCommandLeaseAsync(command, cancellationToken).ConfigureAwait(false);
        var value = await lease.Command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        CopyOutputParameterValues(lease.Command, command.Parameters);
        return ConvertValue<T>(value);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);

        await using var lease = await CreateCommandLeaseAsync(command, cancellationToken).ConfigureAwait(false);
        await using var reader = await lease.Command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(map(reader));
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        CopyOutputParameterValues(lease.Command, command.Parameters);
        return results;
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(map);

        await using var lease = await CreateCommandLeaseAsync(command, cancellationToken).ConfigureAwait(false);
        await using var reader = await lease.Command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await reader.DisposeAsync().ConfigureAwait(false);
            CopyOutputParameterValues(lease.Command, command.Parameters);
            return default;
        }

        var result = map(reader);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The query returned more than one row.");
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        CopyOutputParameterValues(lease.Command, command.Parameters);
        return result;
    }

    private async ValueTask<CommandLease> CreateCommandLeaseAsync(
        SqlCommandDefinition definition,
        CancellationToken cancellationToken)
    {
        if (transaction != null)
        {
            DbCommand transactionCommand = CreateCommand(transaction.Connection, definition);
            transactionCommand.Transaction = transaction.Transaction;
            return new CommandLease(transactionCommand, null);
        }

        DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return new CommandLease(CreateCommand(connection, definition), connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    protected virtual DbCommand CreateCommand(DbConnection connection, SqlCommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Sql);

        var command = connection.CreateCommand();
        try
        {
            command.CommandText = definition.Sql;
            command.CommandType = definition.CommandType;
            int? timeout = definition.TimeoutSeconds ?? transaction?.CommandTimeoutSeconds ?? connectionFactory?.CommandTimeoutSeconds;
            if (timeout.HasValue)
            {
                if (timeout.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(definition), timeout, "Command timeout must be greater than or equal to 0.");
                }

                command.CommandTimeout = timeout.Value;
            }

            AddParameters(command, definition.Parameters);
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
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

            if (parameter.Size.HasValue)
            {
                dbParameter.Size = parameter.Size.Value;
            }

            if (parameter.Precision.HasValue)
            {
                dbParameter.Precision = parameter.Precision.Value;
            }

            if (parameter.Scale.HasValue)
            {
                dbParameter.Scale = parameter.Scale.Value;
            }

            command.Parameters.Add(dbParameter);
        }
    }

    protected static void CopyOutputParameterValues(DbCommand command, IReadOnlyList<SqlParameterValue>? parameters)
    {
        if (parameters == null)
        {
            return;
        }

        for (int index = 0; index < parameters.Count; index++)
        {
            SqlParameterValue parameter = parameters[index];
            if (parameter.Direction != System.Data.ParameterDirection.Input)
            {
                object? value = command.Parameters[index].Value;
                parameter.Value = value == DBNull.Value ? null : value;
            }
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

    private sealed class CommandLease : IAsyncDisposable
    {
        private readonly DbConnection? ownedConnection;

        public CommandLease(DbCommand command, DbConnection? ownedConnection)
        {
            Command = command;
            this.ownedConnection = ownedConnection;
        }

        public DbCommand Command { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Command.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                if (ownedConnection != null)
                {
                    await ownedConnection.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
