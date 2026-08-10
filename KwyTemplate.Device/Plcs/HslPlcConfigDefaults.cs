using Kwy.Communicate.Abstractions.Enums;
using Kwy.Device.Abstractions.PLC;
using Kwy.Device.PLCs.Hsl;

namespace KwyTemplate.Device.Plcs;

/// <summary>
/// HSL PLC 常用默认连接配置集合。Catalog 可以按机型选择其中一种默认配置。
/// </summary>
internal static class HslPlcConfigDefaults
{
    public static HslPlcConfig CreateKeyenceKv8000MainPlc()
    {
        var config = new HslPlcConfig();
        ApplyKeyenceKv8000MainPlc(config);
        return config;
    }

    public static void ApplyKeyenceKv8000MainPlc(HslPlcConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.Brand = HslPlcBrandType.Keyence_NanoSerialOverTcp;
        config.Transport = PlcConnectionTransport.Tcp;
        config.IpAddress = "192.168.0.10";
        config.Port = 8501;
        config.ConnectTimeoutMilliseconds = 5000;
        config.ReceiveTimeoutMilliseconds = 5000;
        config.KeepAlive = false;
    }

    public static HslPlcConfig CreatePanasonicFpXhSerialPlc()
    {
        var config = new HslPlcConfig();
        ApplyPanasonicFpXhSerialPlc(config);
        return config;
    }

    public static void ApplyPanasonicFpXhSerialPlc(HslPlcConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.Brand = HslPlcBrandType.Modbus_Rtu;
        config.Transport = PlcConnectionTransport.Serial;
        config.PortName = "COM6";
        config.BaudRate = 9600;
        config.DataBits = 8;
        config.Parity = ParityType.None;
        config.StopBits = StopBitsType.One;
        config.Station = 238;
        config.KeepAlive = false;
    }
}
