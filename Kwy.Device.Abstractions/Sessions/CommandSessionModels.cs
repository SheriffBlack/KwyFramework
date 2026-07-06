namespace Kwy.Device.Abstractions.Sessions;

public sealed record CommandRequest(
    string Name,
    ReadOnlyMemory<byte> Payload,
    TimeSpan Timeout);

public sealed record CommandResponse(
    string Name,
    ReadOnlyMemory<byte> Payload,
    bool IsAck,
    string? ErrorMessage = null);

public sealed record PendingTransaction(
    string TransactionId,
    string CommandName,
    DateTimeOffset CreatedAt);
