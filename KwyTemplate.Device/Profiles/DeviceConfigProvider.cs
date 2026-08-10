using System.Collections.Concurrent;
using System.Reflection;
using System.IO;
using Kwy.Files;

namespace KwyTemplate.Device.Profiles;

public interface IDeviceConfigProvider
{
    TConfig GetOrCreate<TConfig>(string catalogKey, string deviceId, Func<TConfig> factory)
        where TConfig : class;

    IReadOnlyCollection<DeviceConfigEntry> GetEntries();

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);
}

public sealed record DeviceConfigEntry(string CatalogKey, string DeviceId, Type ConfigType, object Config);

/// <summary>
/// 设备配置来源：按 CatalogKey + DeviceId 管理强类型配置，并负责一设备一 JSON 的持久化读写。
/// 不在接口中暴露具体设备配置类型，避免设备数量增加后接口膨胀。
/// </summary>
public sealed class DeviceConfigProvider : IDeviceConfigProvider
{
    private readonly ConcurrentDictionary<string, DeviceConfigEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim fileLock = new(1, 1);
    private readonly string configRootDirectory = Path.Combine(AppContext.BaseDirectory, "Config");

    public TConfig GetOrCreate<TConfig>(string catalogKey, string deviceId, Func<TConfig> factory)
        where TConfig : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(factory);

        string normalizedCatalogKey = ToSafeFileName(catalogKey);
        string entryKey = CreateEntryKey(normalizedCatalogKey, deviceId);
        DeviceConfigEntry entry = entries.GetOrAdd(
            entryKey,
            static (_, state) =>
            {
                TConfig config = state.Provider.LoadOrCreate(state.CatalogKey, state.DeviceId, state.Factory);
                return new DeviceConfigEntry(state.CatalogKey, state.DeviceId, typeof(TConfig), config);
            },
            (Provider: this, CatalogKey: normalizedCatalogKey, DeviceId: deviceId, Factory: factory));

        if (entry.Config is TConfig typedConfig)
        {
            return typedConfig;
        }

        throw new InvalidOperationException($"Device config type mismatch. CatalogKey={entry.CatalogKey}, DeviceId={deviceId}, Existing={entry.ConfigType.FullName}, Requested={typeof(TConfig).FullName}.");
    }

    public IReadOnlyCollection<DeviceConfigEntry> GetEntries()
        => entries.Values
            .OrderBy(static entry => entry.CatalogKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (DeviceConfigEntry entry in entries.Values)
            {
                await JsonHelper.WriteAsync(GetConfigPath(entry.CatalogKey, entry.DeviceId), entry.Config).ConfigureAwait(false);
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (DeviceConfigEntry entry in entries.Values.ToArray())
            {
                object? loaded = await ReadConfigAsync(entry.ConfigType, GetConfigPath(entry.CatalogKey, entry.DeviceId)).ConfigureAwait(false);
                if (loaded != null)
                {
                    entries[CreateEntryKey(entry.CatalogKey, entry.DeviceId)] = entry with { Config = loaded };
                    await JsonHelper.WriteAsync(GetConfigPath(entry.CatalogKey, entry.DeviceId), loaded).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            fileLock.Release();
        }
    }

    private TConfig LoadOrCreate<TConfig>(string catalogKey, string deviceId, Func<TConfig> factory)
        where TConfig : class
    {
        string path = GetConfigPath(catalogKey, deviceId);
        if (File.Exists(path))
        {
            TConfig? loaded = JsonHelper.ReadAsync<TConfig>(path).GetAwaiter().GetResult();
            if (loaded != null)
            {
                JsonHelper.WriteAsync(path, loaded).GetAwaiter().GetResult();
                return loaded;
            }
        }

        TConfig config = factory();
        JsonHelper.WriteAsync(path, config).GetAwaiter().GetResult();
        return config;
    }
    private async Task<object?> ReadConfigAsync(Type configType, string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        MethodInfo method = typeof(JsonHelper)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(static method =>
            {
                if (method.Name != nameof(JsonHelper.ReadAsync) || !method.IsGenericMethodDefinition)
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].HasDefaultValue;
            })
            .MakeGenericMethod(configType);

        object? taskObject = method.Invoke(null, [path, null]);
        if (taskObject is not Task task)
        {
            return null;
        }

        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private string GetConfigPath(string catalogKey, string deviceId)
        => Path.Combine(configRootDirectory, ToSafeFileName(catalogKey), $"{ToSafeFileName(deviceId)}.json");

    private static string CreateEntryKey(string catalogKey, string deviceId)
        => $"{catalogKey}::{deviceId}";

    private static string ToSafeFileName(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
