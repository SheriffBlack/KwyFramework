using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Device.PLC.Abstractions;
using Kwy.Device.PLC.Hsl;
using KwyTemplate.Device.Connections.Editors;

namespace KwyTemplate.Device.Plcs.Editors;

/// <summary>
/// UI metadata wrapper for <see cref="HslPlcConfig" />.
/// </summary>
public sealed class HslPlcConfigEditorModel
{
    private readonly HslPlcConfig source;

    public HslPlcConfigEditorModel(HslPlcConfig source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    [Browsable(false)]
    public HslPlcConfig Source => source;

    [Browsable(false)]
    public TcpConnectionEditorModel Tcp => new(
        () => GetTcpConnection().Host,
        value => GetTcpConnection().Host = value,
        () => GetTcpConnection().Port,
        value => GetTcpConnection().Port = value,
        () => source.ConnectTimeoutMilliseconds,
        value => source.ConnectTimeoutMilliseconds = value,
        () => source.ReceiveTimeoutMilliseconds,
        value => source.ReceiveTimeoutMilliseconds = value);

    [Browsable(false)]
    public SerialConnectionEditorModel Serial => new(
        () => GetSerialConnection().PortName,
        value => GetSerialConnection().PortName = value,
        () => GetSerialConnection().BaudRate,
        value => GetSerialConnection().BaudRate = value,
        () => GetSerialConnection().DataBits,
        value => GetSerialConnection().DataBits = value,
        () => GetSerialConnection().Parity,
        value => GetSerialConnection().Parity = value,
        () => GetSerialConnection().StopBits,
        value => GetSerialConnection().StopBits = value);

    [Category("PLC协议")]
    [CategoryKey("Plc.Category.Protocol")]
    [DisplayName("PLC品牌")]
    [DisplayNameKey("Plc.Brand")]
    public HslPlcBrandType Brand
    {
        get => source.Brand;
        set => source.Brand = value;
    }

    [Category("PLC协议")]
    [CategoryKey("Plc.Category.Protocol")]
    [DisplayName("连接方式")]
    [DisplayNameKey("Plc.Transport")]
    [InputType(InputType.RadioButton)]
    public HslConnectionEditorKind ConnectionKind
    {
        get => source.Connection switch
        {
            TcpPlcConnectionConfig => HslConnectionEditorKind.Tcp,
            SerialPlcConnectionConfig => HslConnectionEditorKind.Serial,
            _ => throw new InvalidOperationException("当前 HSL PLC 连接配置不受编辑器支持。")
        };
        set
        {
            if (value == ConnectionKind)
            {
                return;
            }

            source.Connection = value switch
            {
                HslConnectionEditorKind.Tcp => new TcpPlcConnectionConfig(),
                HslConnectionEditorKind.Serial => new SerialPlcConnectionConfig(),
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
        }
    }

    [Category("PLC协议")]
    [CategoryKey("Plc.Category.Protocol")]
    [DisplayName("站号")]
    [DisplayNameKey("Plc.Station")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 255, SmallChange = 1, DecimalPlaces = 0)]
    public byte Station
    {
        get => source.Station;
        set => source.Station = value;
    }

    [Category("Siemens")]
    [CategoryKey("Plc.Category.Siemens")]
    [DisplayName("Rack")]
    [DisplayNameKey("Plc.Rack")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 255, SmallChange = 1, DecimalPlaces = 0)]
    public byte Rack
    {
        get => source.Rack;
        set => source.Rack = value;
    }

    [Category("Siemens")]
    [CategoryKey("Plc.Category.Siemens")]
    [DisplayName("Slot")]
    [DisplayNameKey("Plc.Slot")]
    [InputType(InputType.NumberBox)]
    [NumberRange(0, 255, SmallChange = 1, DecimalPlaces = 0)]
    public byte Slot
    {
        get => source.Slot;
        set => source.Slot = value;
    }

    [Category("PLC心跳")]
    [CategoryKey("Plc.Category.KeepAlive")]
    [DisplayName("启用心跳")]
    [DisplayNameKey("Connection.KeepAlive")]
    public bool KeepAlive
    {
        get => source.KeepAlive;
        set => source.KeepAlive = value;
    }

    [Category("PLC心跳")]
    [CategoryKey("Plc.Category.KeepAlive")]
    [DisplayName("心跳间隔(ms)")]
    [DisplayNameKey("Connection.KeepAliveInterval")]
    [InputType(InputType.NumberBox)]
    [NumberRange(1, 600000, SmallChange = 100, DecimalPlaces = 0)]
    public int KeepAliveInterval
    {
        get => source.KeepAliveInterval;
        set => source.KeepAliveInterval = value;
    }

    [Category("PLC心跳")]
    [CategoryKey("Plc.Category.KeepAlive")]
    [DisplayName("心跳地址")]
    [DisplayNameKey("Plc.KeepAliveAddress")]
    public string? KeepAliveAddress
    {
        get => source.KeepAliveAddress;
        set => source.KeepAliveAddress = value;
    }

    [Category("PLC心跳")]
    [CategoryKey("Plc.Category.KeepAlive")]
    [DisplayName("心跳模式")]
    [DisplayNameKey("Plc.KeepAliveMode")]
    public PlcKeepAliveMode KeepAliveMode
    {
        get => source.KeepAliveMode;
        set => source.KeepAliveMode = value;
    }

    public IReadOnlyList<object> CreatePropertyGridSources()
        => ConnectionKind == HslConnectionEditorKind.Serial
            ? [this, Serial]
            : [this, Tcp];

    private TcpPlcConnectionConfig GetTcpConnection()
        => source.Connection as TcpPlcConnectionConfig
            ?? throw new InvalidOperationException("当前 HSL PLC 未配置 TCP 连接。");

    private SerialPlcConnectionConfig GetSerialConnection()
        => source.Connection as SerialPlcConnectionConfig
            ?? throw new InvalidOperationException("当前 HSL PLC 未配置串口连接。");
}

/// <summary>HSL PLC 属性编辑器使用的连接方式选项。</summary>
public enum HslConnectionEditorKind
{
    /// <summary>TCP 网络连接。</summary>
    Tcp,

    /// <summary>串口连接。</summary>
    Serial
}

