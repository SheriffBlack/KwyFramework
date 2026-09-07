using System.Text.Json;
using System.IO;
using KwyTemplate.Device.Profiles;

namespace KwyTemplate.App.Services;

/// <summary>SystemView editor persistence for the same MachineProfile consumed at the next application startup.</summary>
public sealed class MachineProfileEditorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly IMachineRuntimeOptionsProvider runtimeOptionsProvider;
    private readonly IMachineProfileProvider profileProvider;

    public MachineProfileEditorStore(
        IMachineRuntimeOptionsProvider runtimeOptionsProvider,
        IMachineProfileProvider profileProvider)
    {
        this.runtimeOptionsProvider = runtimeOptionsProvider ?? throw new ArgumentNullException(nameof(runtimeOptionsProvider));
        this.profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
    }

    public string FilePath => GetFilePath(ProfileKey);
    public string RuntimeOptionsFilePath => Path.Combine(AppContext.BaseDirectory, "Config", "System", "MachineRuntimeOptions.json");

    /// <summary>
    /// Returns only generic-machine profiles. DeviceCatalog folders belong to the legacy/special-machine
    /// implementation and must not be offered as configurable machines.
    /// </summary>
    public IReadOnlyList<string> GetConfigurableProfileKeys()
    {
        string configRoot = Path.Combine(AppContext.BaseDirectory, "Config");
        if (!Directory.Exists(configRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(configRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(key => !string.IsNullOrWhiteSpace(key)
                && !string.Equals(key, "System", StringComparison.OrdinalIgnoreCase)
                && !key.EndsWith("_DeviceCatalog", StringComparison.OrdinalIgnoreCase))
            .Where(key => File.Exists(Path.Combine(configRoot, key!, "MachineProfile.json")))
            .Where(key => IsReadableProfile(Path.Combine(configRoot, key!, "MachineProfile.json"), key!))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    public string GetFilePath(string profileKey)
    {
        ValidateProfileKey(profileKey);
        return Path.Combine(AppContext.BaseDirectory, "Config", profileKey, "MachineProfile.json");
    }

    public string ProfileKey
    {
        get
        {
            MachineRuntimeOptions options = runtimeOptionsProvider.Get();
            if (!string.Equals(options.ActiveMachineKey, MachineRuntimeOptions.ConfigurableMachineKey, StringComparison.OrdinalIgnoreCase))
            {
                return options.ActiveMachineKey switch
                {
                    "Machine_2_A" => "Machine_2_A_DeviceCatalog",
                    "Machine_4_HAHH" => "Machine_4_HAHH_DeviceCatalog",
                    _ => "Machine_Default_DeviceCatalog"
                };
            }

            return string.IsNullOrWhiteSpace(options.ActiveProfileKey) ? "Default" : options.ActiveProfileKey;
        }
    }

    public MachineProfile LoadOrCreate()
        => LoadOrCreate(ProfileKey);

    public MachineProfile LoadOrCreate(string profileKey)
    {
        ValidateProfileKey(profileKey);
        string filePath = GetFilePath(profileKey);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        if (File.Exists(filePath))
        {
            MachineProfile? loadedProfile = JsonSerializer.Deserialize<MachineProfile>(File.ReadAllText(filePath), JsonOptions);
            if (loadedProfile != null)
            {
                return loadedProfile;
            }
        }

        MachineProfile source = profileProvider.GetActiveProfile();
        var profile = JsonSerializer.Deserialize<MachineProfile>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? new MachineProfile();
        profile.ProfileKey = profileKey;
        if (string.IsNullOrWhiteSpace(profile.MachineId))
        {
            profile.MachineId = profileKey;
        }

        SaveAsync(profile).GetAwaiter().GetResult();
        return profile;
    }

    public MachineRuntimeOptions LoadRuntimeOptions()
    {
        if (File.Exists(RuntimeOptionsFilePath))
        {
            MachineRuntimeOptions? loaded = JsonSerializer.Deserialize<MachineRuntimeOptions>(File.ReadAllText(RuntimeOptionsFilePath), JsonOptions);
            if (loaded != null)
            {
                return loaded;
            }
        }

        MachineRuntimeOptions source = runtimeOptionsProvider.Get();
        return JsonSerializer.Deserialize<MachineRuntimeOptions>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? new MachineRuntimeOptions();
    }

    public async Task SaveAsync(MachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfileKey(profile.ProfileKey);
        MachineProfileValidator.Validate(profile);
        string path = GetFilePath(profile.ProfileKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(profile, JsonOptions)).ConfigureAwait(false);
    }

    public async Task SaveRuntimeOptionsAsync(MachineRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Directory.CreateDirectory(Path.GetDirectoryName(RuntimeOptionsFilePath)!);
        await File.WriteAllTextAsync(RuntimeOptionsFilePath, JsonSerializer.Serialize(options, JsonOptions)).ConfigureAwait(false);
    }

    private static void ValidateProfileKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("机种标识不能为空，且不能包含文件名非法字符或路径分隔符。");
        }
    }

    private static bool IsReadableProfile(string path, string profileKey)
    {
        try
        {
            MachineProfile? profile = JsonSerializer.Deserialize<MachineProfile>(File.ReadAllText(path), JsonOptions);
            return profile != null && string.Equals(profile.ProfileKey, profileKey, StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
