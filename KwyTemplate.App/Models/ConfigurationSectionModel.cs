namespace KwyTemplate.App.Models;

public sealed class ConfigurationSectionModel
{
    public ConfigurationSectionModel(string title, string description, object source)
    {
        Title = title;
        Description = description;
        Source = source;
    }

    public string Title { get; }

    public string Description { get; }

    public object Source { get; }
}
