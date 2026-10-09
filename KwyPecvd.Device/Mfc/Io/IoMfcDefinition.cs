namespace KwyPecvd.Device.Mfc.Io;

public sealed record IoMfcDefinition
{
    public required MfcDefinition Mfc { get; init; }
    public required string SetPointAoId { get; init; }
    public required string FeedbackAiId { get; init; }
    public string? OfflineDiId { get; init; }
    public string? OutputOwner { get; init; }

    public void Validate()
    {
        Mfc.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(SetPointAoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(FeedbackAiId);
        if (OfflineDiId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(OfflineDiId);
    }
}
