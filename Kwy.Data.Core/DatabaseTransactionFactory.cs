using System.Data;
using Kwy.Data.Abstractions;

namespace Kwy.Data.Core;

public sealed class DatabaseTransactionFactory : IDatabaseTransactionFactory
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public DatabaseTransactionFactory(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async ValueTask<IDatabaseTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            return new DatabaseTransaction(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
