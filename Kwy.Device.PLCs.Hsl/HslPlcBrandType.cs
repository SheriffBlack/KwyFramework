namespace Kwy.Device.PLCs.Hsl;

/// <summary>
/// PLC brand or protocol type supported by the HslCommunication based driver.
/// </summary>
public enum HslPlcBrandType
{
    // Siemens S7 over TCP.
    Siemens_S71200 = 0,
    Siemens_S71500 = 1,
    Siemens_S7300 = 2,
    Siemens_S7400 = 3,
    Siemens_S7200Smart = 4,

    // Mitsubishi protocols.
    Mitsubishi_MC = 5,
    Mitsubishi_Fx3U = 6,
    Mitsubishi_Fx5U = 7,
    Mitsubishi_FxSerial = 8,

    // Omron FINS over TCP.
    Omron_Fins = 9,

    // Keyence MC-compatible TCP.
    Keyence_MC = 10,

    // Keyence upper-link/Nano serial protocol over TCP.
    Keyence_NanoSerialOverTcp = 11,

    // Panasonic MC over TCP.
    Panasonic_MC = 12,

    // Panasonic MEWTOCOL over serial.
    Panasonic_Mewtocol = 13,

    // Modbus protocols.
    Modbus_Tcp = 14,
    Modbus_Rtu = 15
}