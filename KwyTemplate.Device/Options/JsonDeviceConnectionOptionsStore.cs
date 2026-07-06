using Kwy.Files;
using KwyTemplate.Device.Tcp;
using System.Text.Json;

namespace KwyTemplate.Device.Options;

public sealed class JsonDeviceConnectionOptionsStore : IDeviceConnectionOptionsStore
{
    private const string DefaultFileName = "device-connections.json";
    private readonly SemaphoreSlim sync = new(1, 1);
    private readonly string filePath;

    public JsonDeviceConnectionOptionsStore()
        : this(Path.Combine(AppContext.BaseDirectory, "Config", DefaultFileName))
    {
    }

    public JsonDeviceConnectionOptionsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public async ValueTask<DeviceConnectionOptions> LoadAsync(CancellationToken cancellationToken = default)
    {
        await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                var defaults = new DeviceConnectionOptions();
                await SaveCoreAsync(defaults).ConfigureAwait(false);
                return DeviceConnectionOptionsCloner.Clone(defaults);
            }

            string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            bool isLegacyOptions = IsLegacyOptions(json);

            DeviceConnectionOptions options = isLegacyOptions
                ? ReadLegacyOptions(json)
                : await JsonHelper.ReadAsync<DeviceConnectionOptions>(filePath).ConfigureAwait(false) ?? new DeviceConnectionOptions();

            DeviceConnectionOptionsNormalizer.Normalize(options, useRtuDefaults: isLegacyOptions);
            if (isLegacyOptions)
            {
                await SaveCoreAsync(options).ConfigureAwait(false);
            }

            return DeviceConnectionOptionsCloner.Clone(options);
        }
        finally
        {
            sync.Release();
        }
    }

    public async ValueTask SaveAsync(DeviceConnectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(DeviceConnectionOptionsCloner.Clone(options)).ConfigureAwait(false);
        }
        finally
        {
            sync.Release();
        }
    }

    private async Task SaveCoreAsync(DeviceConnectionOptions options)
    {
        DeviceConnectionOptionsNormalizer.Normalize(options);
        await JsonHelper.WriteAsync(filePath, options).ConfigureAwait(false);
    }

    private static bool IsLegacyOptions(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        return !document.RootElement.TryGetProperty("Devices", out _)
            && (document.RootElement.TryGetProperty("MainPlc", out _)
                || document.RootElement.TryGetProperty("ExternalTcpDevice", out _));
    }

    private static DeviceConnectionOptions ReadLegacyOptions(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var options = new DeviceConnectionOptions { Devices = [] };

        if (document.RootElement.TryGetProperty("MainPlc", out JsonElement mainPlcElement))
        {
            HslPlcConnectionOptions? mainPlc = mainPlcElement.Deserialize<HslPlcConnectionOptions>(JsonHelper.DefaultOptions);
            options.Devices.Add(DeviceConnectionEntry.Create(
                DeviceIds.MainPlc,
                DeviceConnectionDeviceTypes.HslPlc,
                mainPlc ?? new HslPlcConnectionOptions(),
                connectOnStartup: true));
        }

        if (document.RootElement.TryGetProperty("ExternalTcpDevice", out JsonElement externalTcpElement))
        {
            ExternalTcpDeviceConnectionOptions? externalTcp = externalTcpElement.Deserialize<ExternalTcpDeviceConnectionOptions>(JsonHelper.DefaultOptions);
            options.Devices.Add(DeviceConnectionEntry.Create(
                DeviceIds.ExternalTcpDevice,
                DeviceConnectionDeviceTypes.ExternalTcp,
                externalTcp ?? new ExternalTcpDeviceConnectionOptions(),
                connectOnStartup: false,
                enabled: false));
        }

        return options;
    }
}
