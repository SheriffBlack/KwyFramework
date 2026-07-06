using System.Data;

namespace Kwy.Data.Abstractions;

public interface IDatabaseTransactionFactory
{
    ValueTask<IDatabaseTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
