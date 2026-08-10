using System.ComponentModel;
using Kwy.ComponentModel;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.PLCs.Hsl;
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
        Tcp = new TcpConnectionEditorModel(
            () => source.IpAddress,
            value => source.IpAddress = value,
            () => source.Port,
            value => source.Port = value,
            () => source.ConnectTimeoutMilliseconds,
            value => source.ConnectTimeoutMilliseconds = value,
            () => source.ReceiveTimeoutMilliseconds,
            value => source.ReceiveTimeoutMilliseconds = value);

        Serial = new SerialConnectionEditorModel(
            () => source.PortName,
            value => source.PortName = value,
            () => source.BaudRate,
            value => source.BaudRate = value,
            () => source.DataBits,
            value => source.DataBits = value,
            () => source.Parity,
            value => source.Parity = value,
            () => source.StopBits,
            value => source.StopBits = value);
    }

    [Browsable(false)]
    public HslPlcConfig Source => source;

    [Browsable(false)]
    public TcpConnectionEditorModel Tcp { get; }

    [Browsable(false)]
    public SerialConnectionEditorModel Serial { get; }

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
    public PlcConnectionTransport Transport
    {
        get => source.Transport;
        set => source.Transport = value;
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
        => Transport == PlcConnectionTransport.Serial
            ? [this, Serial]
            : [this, Tcp];
}

