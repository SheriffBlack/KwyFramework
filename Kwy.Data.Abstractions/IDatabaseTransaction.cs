using System.Data.Common;

namespace Kwy.Data.Abstractions;

public interface IDatabaseTransaction : IAsyncDisposable
{
    DbConnection Connection { get; }

    DbTransaction Transaction { get; }

    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
