namespace Kwy.Device.Abstractions.Motion;

public interface IAxis
{
    short AxisNumber { get; }
    double Position { get; }
    double EncoderPosition { get; }
    double Velocity { get; }
    bool IsMoving { get; }
    bool IsAlarm { get; }
    bool IsPositiveLimit { get; }
    bool IsNegativeLimit { get; }
}
