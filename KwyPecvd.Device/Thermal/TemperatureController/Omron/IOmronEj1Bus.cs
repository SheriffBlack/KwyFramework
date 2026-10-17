namespace KwyPecvd.Device.Thermal.TemperatureController.Omron;

public interface IOmronEj1Bus
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task<short> ReadHoldingRegisterAsync(
        byte unitAddress,
        ushort register,
        CancellationToken cancellationToken = default);

    Task WriteSingleRegisterAsync(
        byte unitAddress,
        ushort register,
        ushort value,
        CancellationToken cancellationToken = default);
}