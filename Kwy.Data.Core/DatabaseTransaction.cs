using System.Data.Common;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

public sealed class DatabaseTransaction : IDatabaseTransaction
{
    private bool completed;

    public DatabaseTransaction(DbConnection connection, DbTransaction transaction)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public DbConnection Connection { get; }

    public DbTransaction Transaction { get; }

    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        await Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!completed)
        {
            await Transaction.RollbackAsync().ConfigureAwait(false);
        }

        await Transaction.DisposeAsync().ConfigureAwait(false);
        await Connection.DisposeAsync().ConfigureAwait(false);
    }
}
