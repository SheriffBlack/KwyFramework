using Kwy.Device.Abstractions;
using KwyTemplate.Device.Options;
using KwyTemplate.Device.Tcp;

namespace KwyTemplate.Device.Connections;

public sealed class ExternalTcpDeviceConnectionFactory : IDeviceConnectionFactory
{
    public string DeviceType => DeviceConnectionDeviceTypes.ExternalTcp;

    public IDevice Create(DeviceConnectionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ExternalTcpDeviceConnectionOptions config = entry.GetConfig<ExternalTcpDeviceConnectionOptions>();
        if (!config.Validate())
        {
            throw new InvalidOperationException($"External TCP device connection configuration validation failed: {entry.DeviceId}.");
        }

        return new ExternalTcpDevice(entry.DeviceId, config.DeviceName, config);
    }

    public bool IsSameDevice(IDevice device, DeviceConnectionEntry entry)
    {
        if (device is not ExternalTcpDevice tcp)
        {
            return false;
        }

        ExternalTcpDeviceConnectionOptions config = entry.GetConfig<ExternalTcpDeviceConnectionOptions>();
        if (!string.Equals(tcp.DeviceName, config.DeviceName, StringComparison.Ordinal))
        {
            return false;
        }

        return tcp.DeviceParameter is ExternalTcpDeviceConnectionOptions current
            && string.Equals(current.Host, config.Host, StringComparison.OrdinalIgnoreCase)
            && current.Port == config.Port
            && current.KeepAlive == config.KeepAlive
            && current.KeepAliveInterval == config.KeepAliveInterval
            && current.Timeout == config.Timeout
            && current.ReceiveTimeout == config.ReceiveTimeout
            && current.SendTimeout == config.SendTimeout
            && current.ReceiveBufferSize == config.ReceiveBufferSize
            && current.SendBufferSize == config.SendBufferSize
            && current.AutoReconnect == config.AutoReconnect
            && current.MaxReconnectAttempts == config.MaxReconnectAttempts
            && current.ReconnectInterval == config.ReconnectInterval;
    }

    public DeviceConnectionConfigurationSection CreateConfigurationSection(DeviceConnectionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ExternalTcpDeviceConnectionOptions config = entry.GetConfig<ExternalTcpDeviceConnectionOptions>();
        return new DeviceConnectionConfigurationSection(
            config.DeviceName,
            "配置通过 TCP/IP 接入的外部设备连接参数。",
            config);
    }
}
