namespace KwyPecvd.Device.Pumps;

public sealed record VacuumPumpDefinition
{
    public required string Id { get; init; }

    public required string ModuleId { get; init; }

    public required string DisplayName { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);

        ArgumentException.ThrowIfNullOrWhiteSpace(ModuleId);

        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
    }
}
