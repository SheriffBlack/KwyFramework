using Kwy.Files;
using System.IO;

namespace KwyTemplate.Vision.Services;

public sealed class RecentProjectService
{
    private const int MaxRecentProjectCount = 10;
    private readonly string filePath;

    public RecentProjectService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        filePath = Path.Combine(appData, "Kwy", "Vision", "recent-projects.json");
    }

    public IReadOnlyList<string> Load()
    {
        try
        {
            var items = JsonHelper.Read<List<string>>(filePath) ?? [];
            return Normalize(items);
        }
        catch
        {
            return [];
        }
    }

    public void Add(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        var items = Load().ToList();
        items.RemoveAll(item => string.Equals(item, projectPath, StringComparison.OrdinalIgnoreCase));
        items.Insert(0, projectPath);
        Save(items);
    }

    public void Remove(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return;
        }

        var items = Load().ToList();
        items.RemoveAll(item => string.Equals(item, projectPath, StringComparison.OrdinalIgnoreCase));
        Save(items);
    }

    public void Clear()
        => Save([]);

    private void Save(IEnumerable<string> projectPaths)
        => JsonHelper.Write(filePath, Normalize(projectPaths));

    private static IReadOnlyList<string> Normalize(IEnumerable<string> projectPaths)
        => projectPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentProjectCount)
            .ToArray();
}
