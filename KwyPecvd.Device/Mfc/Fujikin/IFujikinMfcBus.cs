namespace KwyPecvd.Device.Mfc.Fujikin;

/// <summary>
/// Fujikin MFC 通信总线。一个总线实例负责串行化同一物理链路上的所有请求。
/// </summary>
public interface IFujikinMfcBus
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task WriteSetPointAsync(
        byte address,
        short rawSetPoint,
        CancellationToken cancellationToken = default);

    Task WriteRampTimeAsync(
        byte address,
        int rawRampTime,
        CancellationToken cancellationToken = default);

    Task<short> ReadIndicatedFlowAsync(
        byte address,
        CancellationToken cancellationToken = default);
}
