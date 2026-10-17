namespace KwyPecvd.Device.RfPowerSupply.Mks;

public readonly record struct MksRfPowerStatus(
    double SetPoint,
    double ForwardPower,
    double ReflectedPower,
    double PaVoltage,
    double Frequency);
