using Kwy.Communicate.TcpSerial.Configs;
using Kwy.Device.Abstractions;
using KwyTemplate.Device.Options;
using System.ComponentModel;

namespace KwyTemplate.Device.Tcp;

public sealed class ExternalTcpDeviceConnectionOptions : IDeviceConfig
{
    [Category("基础信息")]
    [DisplayName("设备ID")]
    public string DeviceId { get; set; } = DeviceIds.ExternalTcpDevice;

    [Category("基础信息")]
    [DisplayName("设备名称")]
    public string DeviceName { get; set; } = "外部TCP设备";

    [Category("TCP连接")]
    [DisplayName("主机地址")]
    public string Host { get; set; } = "192.168.0.20";

    [Category("TCP连接")]
    [DisplayName("端口")]
    public int Port { get; set; } = 9000;

    [Category("启动策略")]
    [DisplayName("启动时连接")]
    public bool ConnectOnStartup { get; set; }

    [Category("心跳")]
    [DisplayName("启用KeepAlive")]
    [Browsable(false)]
    public bool KeepAlive { get; set; } = true;

    [Category("心跳")]
    [DisplayName("KeepAlive间隔(ms)")]
    public int KeepAliveInterval { get; set; } = 1000;

    [Category("超时")]
    [DisplayName("连接超时(ms)")]
    public int Timeout { get; set; } = 5000;

    [Category("超时")]
    [DisplayName("接收超时(ms)")]
    public int ReceiveTimeout { get; set; } = 30000;

    [Category("超时")]
    [DisplayName("发送超时(ms)")]
    public int SendTimeout { get; set; } = 30000;

    [Category("缓冲区")]
    [DisplayName("接收缓冲区")]
    public int ReceiveBufferSize { get; set; } = 65536;

    [Category("缓冲区")]
    [DisplayName("发送缓冲区")]
    public int SendBufferSize { get; set; } = 65536;

    [Category("重连")]
    [DisplayName("启用自动重连")]
    public bool AutoReconnect { get; set; } = true;

    [Category("重连")]
    [DisplayName("最大重连次数")]
    public int MaxReconnectAttempts { get; set; } = 5;

    [Category("重连")]
    [DisplayName("重连间隔(ms)")]
    public int ReconnectInterval { get; set; } = 2000;

    public TcpConfig ToTcpConfig()
        => new()
        {
            Host = Host,
            Port = Port,
            KeepAlive = KeepAlive,
            KeepAliveInterval = KeepAliveInterval,
            Timeout = Timeout,
            ReceiveTimeout = ReceiveTimeout,
            SendTimeout = SendTimeout,
            ReceiveBufferSize = ReceiveBufferSize,
            SendBufferSize = SendBufferSize,
            AutoReconnect = AutoReconnect,
            MaxReconnectAttempts = MaxReconnectAttempts,
            ReconnectInterval = ReconnectInterval
        };

    public bool Validate()
        => ToTcpConfig().Validate();
}

