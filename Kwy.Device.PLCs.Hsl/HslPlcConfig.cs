using Kwy.Device.Abstractions.PLC;

namespace Kwy.Device.PLCs.Hsl;

public class HslPlcConfig : PlcConfig
{
    /// <summary>
    /// Gets or sets the PLC brand or protocol implemented by HslCommunication.
    /// </summary>
    public HslPlcBrandType Brand { get; set; } = HslPlcBrandType.Siemens_S71200;

    /// <summary>
    /// Gets or sets the Siemens rack number.
    /// </summary>
    public byte Rack { get; set; }

    /// <summary>
    /// Gets or sets the Siemens slot number.
    /// </summary>
    public byte Slot { get; set; } = 1;

    /// <summary>
    /// Gets or sets the PLC station number when using station-based serial protocols.
    /// For example: Modbus RTU slave id or Panasonic MEWTOCOL station number.
    /// </summary>
    public byte Station { get; set; } = 1;

    /// <summary>
    /// Gets or sets the HSL TCP connect timeout in milliseconds.
    /// </summary>
    public int ConnectTimeoutMilliseconds { get; set; } = 3000;

    /// <summary>
    /// Gets or sets the HSL TCP receive timeout in milliseconds.
    /// </summary>
    public int ReceiveTimeoutMilliseconds { get; set; } = 3000;

    public override bool Validate()
    {
        if (!base.Validate())
        {
            return false;
        }

        if (ConnectTimeoutMilliseconds <= 0 || ReceiveTimeoutMilliseconds <= 0)
        {
            return false;
        }

        return Transport switch
        {
            PlcConnectionTransport.Tcp => IsTcpBrand(Brand),
            PlcConnectionTransport.Serial => IsSerialBrand(Brand),
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
