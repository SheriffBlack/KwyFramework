namespace Kwy.Device.Abstractions.Sessions;

public interface ITransactionManager
{
    IReadOnlyCollection<PendingTransaction> PendingTransactions { get; }

    string Create(string commandName);

    bool Complete(string transactionId);

    void Clear();
}
