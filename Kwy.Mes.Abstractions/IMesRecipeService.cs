using Kwy.Mes.Abstractions.Models;

namespace Kwy.Mes.Abstractions;

public interface IMesRecipeService
{
    Task<MesResult<MesRecipe>> GetRecipeAsync(string recipeName, CancellationToken cancellationToken = default);
}
