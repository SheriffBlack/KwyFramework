using Kwy.Device.Abstractions.Sessions;
using System.Collections.Concurrent;

namespace Kwy.Device.Core.Sessions;

public sealed class InMemoryTransactionManager : ITransactionManager
{
    private readonly ConcurrentDictionary<string, PendingTransaction> transactions = new();

    public IReadOnlyCollection<PendingTransaction> PendingTransactions => transactions.Values.ToArray();

    public string Create(string commandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        var transactionId = Guid.NewGuid().ToString("N");
        transactions[transactionId] = new PendingTransaction(transactionId, commandName, DateTimeOffset.Now);
        return transactionId;
    }

    public bool Complete(string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        return transactions.TryRemove(transactionId, out _);
    }

    public void Clear() => transactions.Clear();
}
