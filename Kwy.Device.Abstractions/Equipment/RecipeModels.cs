namespace Kwy.Device.Abstractions.Equipment;

public sealed record RecipeParameter(
    string Name,
    string Value,
    string? Unit = null,
    string? Description = null);

public sealed record EquipmentRecipe(
    string RecipeId,
    string Version,
    IReadOnlyList<RecipeParameter> Parameters,
    string? Name = null);

public sealed record RecipeValidationResult(IReadOnlyList<string> Errors)
{
    public static RecipeValidationResult Success { get; } = new(Array.Empty<string>());

    public bool IsValid => Errors.Count == 0;
}

public sealed record RecipeApplyResult(
    bool IsApplied,
    string? Message = null);

public sealed record RecipeChangeRecord(
    string RecipeId,
    string Version,
    string Operator,
    DateTimeOffset Timestamp,
    string? Reason = null);
