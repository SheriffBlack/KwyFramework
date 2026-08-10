using System.Text.Json;

namespace KwyTemplate.Device.Profiles;

/// <summary>
/// Selects either a code-only exceptional machine or the configurable machine profile.
/// The default deliberately preserves the current production machine.
/// </summary>
public sealed class MachineRuntimeOptions
{
    public const string ConfigurableMachineKey = "Configurable";

    public string ActiveMachineKey { get; set; } = "Machine_4_HAHH";

    public string ActiveProfileKey { get; set; } = "Default";
}

public interface IMachineRuntimeOptionsProvider
{
    MachineRuntimeOptions Get();
}

public sealed class MachineRuntimeOptionsProvider : IMachineRuntimeOptionsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string path = Path.Combine(AppContext.BaseDirectory, "Config", "System", "MachineRuntimeOptions.json");
    private readonly Lazy<MachineRuntimeOptions> options;

    public MachineRuntimeOptionsProvider()
    {
        options = new Lazy<MachineRuntimeOptions>(Load);
    }

    public MachineRuntimeOptions Get() => options.Value;

    private MachineRuntimeOptions Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            MachineRuntimeOptions? loaded = JsonSerializer.Deserialize<MachineRuntimeOptions>(File.ReadAllText(path), JsonOptions);
            if (loaded != null)
            {
                return loaded;
            }
        }

        var defaults = new MachineRuntimeOptions();
        File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
        return defaults;
    }
}
