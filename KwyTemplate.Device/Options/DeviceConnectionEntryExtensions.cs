using System.Text.Json;
using Kwy.Files;

namespace KwyTemplate.Device.Options;

public static class DeviceConnectionEntryExtensions
{
    public static TConfig GetConfig<TConfig>(this DeviceConnectionEntry entry)
        where TConfig : class, new()
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Config is TConfig typed)
        {
            return typed;
        }

        if (entry.Config is JsonElement element)
        {
            TConfig? config = element.Deserialize<TConfig>(JsonHelper.DefaultOptions);
            config ??= new TConfig();
            entry.Config = config;
            return config;
        }

        if (entry.Config != null)
        {
            string json = JsonSerializer.Serialize(entry.Config, JsonHelper.DefaultOptions);
            TConfig? config = JsonSerializer.Deserialize<TConfig>(json, JsonHelper.DefaultOptions);
            config ??= new TConfig();
            entry.Config = config;
            return config;
        }

        var defaultConfig = new TConfig();
        entry.Config = defaultConfig;
        return defaultConfig;
    }
}
