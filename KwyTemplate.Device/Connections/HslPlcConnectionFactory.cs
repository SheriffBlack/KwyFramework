using Kwy.Device.Abstractions;
using Kwy.Device.PLCs.Hsl;
using KwyTemplate.Device.Options;

namespace KwyTemplate.Device.Connections;

public sealed class HslPlcConnectionFactory : IDeviceConnectionFactory
{
    public string DeviceType => DeviceConnectionDeviceTypes.HslPlc;

    public IDevice Create(DeviceConnectionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        HslPlcConnectionOptions options = entry.GetConfig<HslPlcConnectionOptions>();
        HslPlcConfig config = ToHslConfig(options);
        if (!config.Validate())
        {
            throw new InvalidOperationException($"PLC connection configuration validation failed: {entry.DeviceId}.");
        }

        return new HslPlcDevice(entry.DeviceId, options.DeviceName, config);
    }

    public bool IsSameDevice(IDevice device, DeviceConnectionEntry entry)
    {
        if (device is not HslPlcDevice hsl)
        {
            return false;
        }

        HslPlcConnectionOptions options = entry.GetConfig<HslPlcConnectionOptions>();
        HslPlcConfig config = ToHslConfig(options);

        if (!string.Equals(hsl.DeviceName, options.DeviceName, StringComparison.Ordinal))
        {
            return false;
        }

        return hsl.DeviceParameter is HslPlcConfig current
            && current.Brand == config.Brand
            && current.Transport == config.Transport
            && string.Equals(current.IpAddress, config.IpAddress, StringComparison.OrdinalIgnoreCase)
            && current.Port == config.Port
            && current.Rack == config.Rack
            && current.Slot == config.Slot
            && string.Equals(current.PortName, config.PortName, StringComparison.OrdinalIgnoreCase)
            && current.BaudRate == config.BaudRate
            && current.DataBits == config.DataBits
            && current.Parity == config.Parity
            && current.StopBits == config.StopBits
            && current.Station == config.Station
            && current.KeepAlive == config.KeepAlive
            && current.KeepAliveInterval == config.KeepAliveInterval
            && string.Equals(current.KeepAliveAddress, config.KeepAliveAddress, StringComparison.OrdinalIgnoreCase)
            && current.KeepAliveMode == config.KeepAliveMode;
    }

    public DeviceConnectionConfigurationSection CreateConfigurationSection(DeviceConnectionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        HslPlcConnectionOptions config = entry.GetConfig<HslPlcConnectionOptions>();
        return new DeviceConnectionConfigurationSection(
            config.DeviceName,
            "配置 PLC 的品牌、连接方式、网络/串口参数、启动策略和心跳参数。",
            config);
    }

    private static HslPlcConfig ToHslConfig(HslPlcConnectionOptions options)
        => new()
        {
            Brand = options.Brand,
            Transport = options.Transport,
            IpAddress = options.IpAddress,
            Port = options.Port,
            Rack = options.Rack,
            Slot = options.Slot,
            PortName = options.PortName,
            BaudRate = options.BaudRate,
            DataBits = options.DataBits,
            Parity = options.Parity,
            StopBits = options.StopBits,
            Station = options.Station,
            KeepAlive = options.KeepAlive,
            KeepAliveInterval = options.KeepAliveInterval,
            KeepAliveAddress = options.KeepAliveAddress,
            KeepAliveMode = options.KeepAliveMode
        };
}
