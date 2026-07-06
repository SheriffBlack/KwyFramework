using System.Text.Json;
using Kwy.Files;
using KwyTemplate.Device.Tcp;

namespace KwyTemplate.Device.Options;

internal static class DeviceConnectionOptionsCloner
{
    public static DeviceConnectionOptions Clone(DeviceConnectionOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string json = JsonSerializer.Serialize(source, JsonHelper.DefaultOptions);
        DeviceConnectionOptions? clone = JsonSerializer.Deserialize<DeviceConnectionOptions>(json, JsonHelper.DefaultOptions);
        clone ??= new DeviceConnectionOptions();
        DeviceConnectionOptionsNormalizer.Normalize(clone);
        return clone;
    }
}

internal static class DeviceConnectionOptionsNormalizer
{
    public static void Normalize(DeviceConnectionOptions options, bool useRtuDefaults = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Devices ??= [];

        if (options.Devices.Count == 0)
        {
            options.Devices.Add(DeviceConnectionEntry.Create(
                DeviceIds.MainPlc,
                DeviceConnectionDeviceTypes.HslPlc,
                new HslPlcConnectionOptions(),
                connectOnStartup: true,
                displayName: "主PLC"));
        }

        var duplicate = options.Devices
            .Where(static x => !string.IsNullOrWhiteSpace(x.DeviceId))
            .GroupBy(static x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static x => x.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Device connection entry id is duplicated: {duplicate.Key}.");
        }

        foreach (DeviceConnectionEntry entry in options.Devices)
        {
            NormalizeEntry(entry, useRtuDefaults);
        }
    }

    private static void NormalizeEntry(DeviceConnectionEntry entry, bool useRtuDefaults)
    {
        if (string.IsNullOrWhiteSpace(entry.DeviceId))
        {
            throw new InvalidOperationException("Device connection entry id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(entry.DeviceType))
        {
            throw new InvalidOperationException($"Device connection entry '{entry.DeviceId}' type cannot be empty.");
        }

        entry.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.DeviceId : entry.DisplayName;

        switch (entry.DeviceType)
        {
            case DeviceConnectionDeviceTypes.HslPlc:
                NormalizeHslPlc(entry, useRtuDefaults);
                break;
            case DeviceConnectionDeviceTypes.ExternalTcp:
                NormalizeExternalTcp(entry);
                break;
        }
    }

    private static void NormalizeHslPlc(DeviceConnectionEntry entry, bool useRtuDefaults)
    {
        if (entry.Config == null)
        {
            entry.Config = new HslPlcConnectionOptions();
        }

        HslPlcConnectionOptions config = entry.GetConfig<HslPlcConnectionOptions>();
        config.DeviceId = string.IsNullOrWhiteSpace(config.DeviceId) ? entry.DeviceId : config.DeviceId;
        config.DeviceName = string.IsNullOrWhiteSpace(config.DeviceName) ? entry.DisplayName : config.DeviceName;

        entry.DeviceId = config.DeviceId;
        entry.DisplayName = config.DeviceName;
        entry.ConnectOnStartup = config.ConnectOnStartup;

        if (!useRtuDefaults)
        {
            return;
        }

        config.Brand = Kwy.Device.PLCs.Hsl.HslPlcBrandType.Modbus_Rtu;
        config.Transport = Kwy.Device.Abstractions.PLC.PlcConnectionTransport.Serial;
        config.PortName = string.IsNullOrWhiteSpace(config.PortName) ? "COM1" : config.PortName;
        config.BaudRate = config.BaudRate <= 0 ? 9600 : config.BaudRate;
        config.DataBits = config.DataBits is < 5 or > 8 ? 8 : config.DataBits;
        config.Station = config.Station == 0 ? (byte)1 : config.Station;
    }

    private static void NormalizeExternalTcp(DeviceConnectionEntry entry)
    {
        if (entry.Config == null)
        {
            entry.Config = new ExternalTcpDeviceConnectionOptions();
        }

        ExternalTcpDeviceConnectionOptions config = entry.GetConfig<ExternalTcpDeviceConnectionOptions>();
        config.DeviceId = string.IsNullOrWhiteSpace(config.DeviceId) ? entry.DeviceId : config.DeviceId;
        config.DeviceName = string.IsNullOrWhiteSpace(config.DeviceName) ? entry.DisplayName : config.DeviceName;

        entry.DeviceId = config.DeviceId;
        entry.DisplayName = config.DeviceName;
        entry.ConnectOnStartup = config.ConnectOnStartup;
    }
}
