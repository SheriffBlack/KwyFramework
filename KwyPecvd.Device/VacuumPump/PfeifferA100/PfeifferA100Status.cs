namespace KwyPecvd.Device.VacuumPump.PfeifferA100;

public readonly record struct PfeifferA100Status(
    bool IsRunning,
    bool HasWarning,
    bool HasAlarm,
    bool IsOverTemperature);
