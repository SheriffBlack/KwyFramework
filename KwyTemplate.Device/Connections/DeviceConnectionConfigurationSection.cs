namespace KwyTemplate.Device.Connections;

public sealed class DeviceConnectionConfigurationSection
{
    public DeviceConnectionConfigurationSection(string title, string description, object source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(source);

        Title = title;
        Description = description ?? string.Empty;
        Source = source;
    }

    public string Title { get; }

    public string Description { get; }

    public object Source { get; }
}
