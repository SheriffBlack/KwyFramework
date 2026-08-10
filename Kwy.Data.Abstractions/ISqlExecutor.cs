using System.Data.Common;

namespace Kwy.Data.Sql;

public interface ISqlExecutor
{
    Task<int> ExecuteAsync(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default);

    Task<T?> ExecuteScalarAsync<T>(
        SqlCommandDefinition command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(
        SqlCommandDefinition command,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken = default);
}
