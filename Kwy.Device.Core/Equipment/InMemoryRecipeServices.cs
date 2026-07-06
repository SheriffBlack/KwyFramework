using Kwy.Device.Abstractions.Equipment;
using System.Collections.Concurrent;

namespace Kwy.Device.Core.Equipment;

public sealed class InMemoryRecipeRepository : IRecipeRepository
{
    private readonly ConcurrentDictionary<string, EquipmentRecipe> recipes = new();

    public Task<EquipmentRecipe?> GetAsync(string recipeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        cancellationToken.ThrowIfCancellationRequested();
        recipes.TryGetValue(recipeId, out var recipe);
        return Task.FromResult(recipe);
    }

    public Task<IReadOnlyCollection<EquipmentRecipe>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyCollection<EquipmentRecipe>>(recipes.Values.ToArray());
    }

    public Task SaveAsync(EquipmentRecipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();
        recipes[recipe.RecipeId] = recipe;
        return Task.CompletedTask;
    }
}

public sealed class DefaultRecipeValidator : IRecipeValidator
{
    public Task<RecipeValidationResult> ValidateAsync(
        EquipmentRecipe recipe,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();

        List<string>? errors = null;
        if (string.IsNullOrWhiteSpace(recipe.RecipeId))
        {
            (errors ??= new List<string>()).Add("RecipeId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Version))
        {
            (errors ??= new List<string>()).Add("Recipe version cannot be empty.");
        }

        return Task.FromResult(errors is null ? RecipeValidationResult.Success : new RecipeValidationResult(errors));
    }
}

public sealed class NoOpRecipeApplier : IRecipeApplier
{
    public Task<RecipeApplyResult> ApplyAsync(
        EquipmentRecipe recipe,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RecipeApplyResult(true, "Recipe validated. No device-specific applier is configured."));
    }
}

public sealed class RecipeService : IRecipeService
{
    private readonly IRecipeRepository repository;
    private readonly IRecipeValidator validator;
    private readonly IRecipeApplier applier;
    private readonly IAuditTrail auditTrail;

    public RecipeService(
        IRecipeRepository repository,
        IRecipeValidator validator,
        IRecipeApplier applier,
        IAuditTrail auditTrail)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
        this.auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
    }

    public EquipmentRecipe? CurrentRecipe { get; private set; }

    public async Task<RecipeApplyResult> LoadAndApplyAsync(
        string recipeId,
        string operatorName,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorName);

        EquipmentRecipe? recipe = await repository.GetAsync(recipeId, cancellationToken);
        if (recipe is null)
        {
            return new RecipeApplyResult(false, $"Recipe {recipeId} was not found.");
        }

        RecipeValidationResult validation = await validator.ValidateAsync(recipe, cancellationToken);
        if (!validation.IsValid)
        {
            return new RecipeApplyResult(false, string.Join("; ", validation.Errors));
        }

        RecipeApplyResult result = await applier.ApplyAsync(recipe, cancellationToken);
        if (result.IsApplied)
        {
            CurrentRecipe = recipe;
            await auditTrail.RecordAsync(new EquipmentAuditRecord(
                "RecipeApplied",
                operatorName,
                $"Recipe {recipe.RecipeId} version {recipe.Version} applied.",
                ReasonToSource(reason)), cancellationToken);
        }

        return result;
    }

    private static string? ReasonToSource(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? null : reason;
}
