using System.ComponentModel;
using Kwy.Communicate.Abstractions.Enums;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.PLCs.Hsl;

namespace KwyTemplate.Device.Options;

public sealed class DeviceConnectionOptions
{
    public List<DeviceConnectionEntry> Devices { get; set; } =
    [
        DeviceConnectionEntry.Create(
            DeviceIds.MainPlc,
            DeviceConnectionDeviceTypes.HslPlc,
            new HslPlcConnectionOptions(),
            connectOnStartup: true)
    ];

    public DeviceConnectionEntry? Find(string deviceId)
        => Devices.FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
}

public sealed class DeviceConnectionEntry
{
    [Category("基础信息")]
    [DisplayName("设备ID")]
    public string DeviceId { get; set; } = string.Empty;

    [Category("基础信息")]
    [DisplayName("设备类型")]
    public string DeviceType { get; set; } = string.Empty;

    [Category("基础信息")]
    [DisplayName("显示名称")]
    public string DisplayName { get; set; } = string.Empty;

    [Category("启动策略")]
    [DisplayName("启用设备")]
    public bool Enabled { get; set; } = true;

    [Category("启动策略")]
    [DisplayName("启动时连接")]
    public bool ConnectOnStartup { get; set; }

    [Browsable(false)]
    public object? Config { get; set; }

    public static DeviceConnectionEntry Create(
        string deviceId,
        string deviceType,
        object config,
        bool connectOnStartup,
        bool enabled = true,
        string? displayName = null)
        => new()
        {
            DeviceId = deviceId,
            DeviceType = deviceType,
            DisplayName = displayName ?? deviceId,
            Config = config,
            ConnectOnStartup = connectOnStartup,
            Enabled = enabled
        };
}

public static class DeviceConnectionDeviceTypes
{
    public const string HslPlc = "HslPlc";

    public const string ExternalTcp = "ExternalTcp";
}

public sealed class HslPlcConnectionOptions
{
    [Category("基础信息")]
    [DisplayName("设备ID")]
    public string DeviceId { get; set; } = DeviceIds.MainPlc;

    [Category("基础信息")]
    [DisplayName("设备名称")]
    public string DeviceName { get; set; } = "主PLC";

    [Category("PLC连接")]
    [DisplayName("PLC品牌")]
    public HslPlcBrandType Brand { get; set; } = HslPlcBrandType.Modbus_Rtu;

    [Category("PLC连接")]
    [DisplayName("连接方式")]
    public PlcConnectionTransport Transport { get; set; } = PlcConnectionTransport.Serial;

    [Category("PLC网络")]
    [DisplayName("IP地址")]
    public string IpAddress { get; set; } = "192.168.0.10";

    [Category("PLC网络")]
    [DisplayName("端口")]
    public int Port { get; set; } = 102;

    [Category("PLC网络")]
    [DisplayName("Rack")]
    public byte Rack { get; set; }

    [Category("PLC网络")]
    [DisplayName("Slot")]
    public byte Slot { get; set; } = 1;

    [Category("PLC串口")]
    [DisplayName("串口号")]
    public string PortName { get; set; } = "COM1";

    [Category("PLC串口")]
    [DisplayName("波特率")]
    public int BaudRate { get; set; } = 9600;

    [Category("PLC串口")]
    [DisplayName("数据位")]
    public int DataBits { get; set; } = 8;

    [Category("PLC串口")]
    [DisplayName("校验位")]
    public ParityType Parity { get; set; } = ParityType.None;

    [Category("PLC串口")]
    [DisplayName("停止位")]
    public StopBitsType StopBits { get; set; } = StopBitsType.One;

    [Category("PLC串口")]
    [DisplayName("站号")]
    public byte Station { get; set; } = 1;

    [Category("启动策略")]
    [DisplayName("启动时连接")]
    public bool ConnectOnStartup { get; set; } = true;

    [Category("心跳")]
    [DisplayName("启用心跳")]
    public bool KeepAlive { get; set; }

    [Category("心跳")]
    [DisplayName("心跳间隔(ms)")]
    public int KeepAliveInterval { get; set; } = 1000;

    [Category("心跳")]
    [DisplayName("心跳地址")]
    public string? KeepAliveAddress { get; set; }

    [Category("心跳")]
    [DisplayName("心跳模式")]
    public PlcKeepAliveMode KeepAliveMode { get; set; } = PlcKeepAliveMode.ReadBool;
}
