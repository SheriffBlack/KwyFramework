using Kwy.Device.Abstractions.IO;

namespace Kwy.Device.IoCards.Advantech;

/// <summary>
/// 1730U 所需 DAQNavi 数字量能力的窄适配端口。
/// 不向上泄漏 DAQNavi 类型，便于以 fake 验证驱动契约。
/// </summary>
public interface IAdvantechIoSdkPort : IDisposable
{
    event EventHandler<int>? DiInterruptReceived;

    int DigitalInputPortCount { get; }

    int DigitalOutputPortCount { get; }

    void Open(string deviceDescription);

    void Close();

    void ConfigureDiInterrupt(int port, IoTriggerEdge triggerEdge);

    void StartDiInterrupt();

    void StopDiInterrupt();

    bool ReadDiBit(int port, int bit);

    void WriteDoBit(int port, int bit, bool state);

    void ReadDiPorts(byte[] destination, int portCount);

    void ReadDoPorts(byte[] destination, int portCount);

    void WriteDoPort(int port, byte value);
}
