using System.IO;

namespace KwyTemplate.Vision.ViewModels.Items;

public sealed class RecentProjectItemViewModel
{
    public RecentProjectItemViewModel(string filePath)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileNameWithoutExtension(filePath);
    }

    public string DisplayName { get; }

    public string FilePath { get; }
}
