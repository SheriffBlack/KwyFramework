using Kwy.Device.PLC.Abstractions;

namespace Kwy.Device.PLC.Hsl;

/// <summary>
/// HslCommunication PLC 驱动配置。
/// <para>HSL 支持 TCP 与串口；通过 <see cref="Connection"/> 选择其中一种强类型连接配置。</para>
/// </summary>
public sealed class HslPlcConfig : PlcDeviceRuntimeConfig
{
    /// <summary>HSL PLC 的物理连接参数。</summary>
    public PlcConnectionConfig Connection { get; set; } = new TcpPlcConnectionConfig();

    /// <summary>HslCommunication 实现的 PLC 品牌或协议类型。</summary>
    public HslPlcBrandType Brand { get; set; } = HslPlcBrandType.Siemens_S71200;

    /// <summary>西门子 PLC 机架号。</summary>
    public byte Rack { get; set; }

    /// <summary>西门子 PLC 槽号。</summary>
    public byte Slot { get; set; } = 1;

    /// <summary>站号型串口协议的 PLC 站号，例如 Modbus RTU 从站号。</summary>
    public byte Station { get; set; } = 1;

    /// <summary>HSL 连接超时，单位为毫秒。</summary>
    public int ConnectTimeoutMilliseconds { get; set; } = 3000;

    /// <summary>HSL 接收超时，单位为毫秒。</summary>
    public int ReceiveTimeoutMilliseconds { get; set; } = 3000;

    public override bool Validate()
    {
        if (!base.Validate())
        {
            return false;
        }

        if (Connection is null || !Connection.Validate()
            || ConnectTimeoutMilliseconds <= 0 || ReceiveTimeoutMilliseconds <= 0)
        {
            return false;
        }

        return Connection switch
        {
            TcpPlcConnectionConfig => IsTcpBrand(Brand),
            SerialPlcConnectionConfig => IsSerialBrand(Brand),
            _ => false
        };
    }

    private static bool IsTcpBrand(HslPlcBrandType brand)
        => brand is HslPlcBrandType.Siemens_S71200
            or HslPlcBrandType.Siemens_S71500
            or HslPlcBrandType.Siemens_S7300
            or HslPlcBrandType.Siemens_S7400
            or HslPlcBrandType.Siemens_S7200Smart
            or HslPlcBrandType.Mitsubishi_MC
            or HslPlcBrandType.Mitsubishi_Fx3U
            or HslPlcBrandType.Mitsubishi_Fx5U
            or HslPlcBrandType.Omron_Fins
            or HslPlcBrandType.Keyence_MC
            or HslPlcBrandType.Keyence_NanoSerialOverTcp
            or HslPlcBrandType.Panasonic_MC
            or HslPlcBrandType.Modbus_Tcp;

    private static bool IsSerialBrand(HslPlcBrandType brand)
        => brand is HslPlcBrandType.Modbus_Rtu
            or HslPlcBrandType.Panasonic_Mewtocol
            or HslPlcBrandType.Mitsubishi_FxSerial;
}
