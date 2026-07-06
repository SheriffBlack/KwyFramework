namespace Kwy.Device.Abstractions.Equipment;

public interface IRecipeRepository
{
    Task<EquipmentRecipe?> GetAsync(string recipeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EquipmentRecipe>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(EquipmentRecipe recipe, CancellationToken cancellationToken = default);
}

public interface IRecipeValidator
{
    Task<RecipeValidationResult> ValidateAsync(
        EquipmentRecipe recipe,
        CancellationToken cancellationToken = default);
}

public interface IRecipeApplier
{
    Task<RecipeApplyResult> ApplyAsync(
        EquipmentRecipe recipe,
        CancellationToken cancellationToken = default);
}

public interface IRecipeService
{
    EquipmentRecipe? CurrentRecipe { get; }

    Task<RecipeApplyResult> LoadAndApplyAsync(
        string recipeId,
        string operatorName,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
