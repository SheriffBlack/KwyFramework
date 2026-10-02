using System.Data;
using System.Data.Common;
using Kwy.Data.Abstractions;
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
        await using var lease = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
        int affectedRows = await lease.Command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        CopyOutputParameterValues(lease.Command, command.Parameters);
        return affectedRows;
    }

    public async Task<T?> ExecuteScalarAsync<T>(SqlCommandDefinition command, CancellationToken cancellationToken = default)
    {
        await using var lease = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
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

        await using var lease = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
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

        await using var lease = await CreateCommandAsync(command, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<CommandLease> CreateCommandAsync(
        SqlCommandDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Sql);

        var connection = dbContext.Database.GetDbConnection();
        bool openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = definition.Sql;
                command.CommandType = definition.CommandType;
                int? timeout = definition.TimeoutSeconds ?? dbContext.Database.GetCommandTimeout();
                if (timeout.HasValue)
                {
                    if (timeout.Value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(definition), timeout, "Command timeout must be greater than or equal to 0.");
                    }

                    command.CommandTimeout = timeout.Value;
                }

                var currentTransaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                if (currentTransaction != null)
                {
                    command.Transaction = currentTransaction;
                }

                AddParameters(command, definition.Parameters);
                return new CommandLease(command, openedHere ? connection : null);
            }
            catch
            {
                await command.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            if (openedHere)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }

            throw;
        }
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

    private static void CopyOutputParameterValues(DbCommand command, IReadOnlyList<SqlParameterValue>? parameters)
    {
        if (parameters == null)
        {
            return;
        }

        for (int index = 0; index < parameters.Count; index++)
        {
            SqlParameterValue parameter = parameters[index];
            if (parameter.Direction != ParameterDirection.Input)
            {
                object? value = command.Parameters[index].Value;
                parameter.Value = value == DBNull.Value ? null : value;
            }
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

    private sealed class CommandLease : IAsyncDisposable
    {
        private readonly DbConnection? connectionToClose;

        public CommandLease(DbCommand command, DbConnection? connectionToClose)
        {
            Command = command;
            this.connectionToClose = connectionToClose;
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
                if (connectionToClose != null)
                {
                    await connectionToClose.CloseAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
