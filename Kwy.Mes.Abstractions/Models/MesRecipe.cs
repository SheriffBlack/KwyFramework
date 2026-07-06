using System.Collections.ObjectModel;

namespace Kwy.Mes.Abstractions.Models;

public sealed record MesRecipe(
    string Name,
    string Revision,
    IReadOnlyDictionary<string, string> Parameters)
{
    public static MesRecipe Empty(string name, string revision = "")
        => new(name, revision, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
}
