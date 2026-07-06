namespace Kwy.Device.PLCs.Hsl;

/// <summary>
/// PLC brand or protocol type supported by the HslCommunication based driver.
/// </summary>
public enum HslPlcBrandType
{
    // Siemens S7 over TCP.
    Siemens_S71200,
    Siemens_S71500,
    Siemens_S7300,
    Siemens_S7400,
    Siemens_S7200Smart,

    // Mitsubishi protocols.
    Mitsubishi_MC,
    Mitsubishi_Fx3U,
    Mitsubishi_Fx5U,
    Mitsubishi_FxSerial,

    // Omron FINS over TCP.
    Omron_Fins,

    // Keyence MC-compatible TCP.
    Keyence_KV,

    // Panasonic MC over TCP.
    Panasonic_MC,

    // Panasonic MEWTOCOL over serial.
    Panasonic_Mewtocol,

    // Modbus protocols.
    Modbus_Tcp,
    Modbus_Rtu
}
