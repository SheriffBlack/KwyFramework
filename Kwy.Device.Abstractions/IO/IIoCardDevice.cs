namespace Kwy.Device.Abstractions.IO;

public interface IDigitalInputDevice
{
    bool ReadDiBit(int channel);
    bool[] ReadAllDi();
    ulong ReadDiPortMask();
}

public interface IDigitalOutputDevice
{
    void WriteDoBit(int channel, bool state);
    void WriteDoPortMask(ulong mask);
    void WriteDoPortMask(ulong mask, ulong changedMask);
    bool[] ReadAllDo();
}

public interface IPulseOutputDevice
{
    void WritePulse(int channel, int durationMs);
}

public interface IHardwareInterruptSource
{
    event EventHandler<ulong> OnHardwareTriggerReceived;
}

public interface IHardwareInterruptWaiter
{
    Task WaitForHardwareInterruptAsync(
        IIoCardDevice device,
        int channel,
        bool expectedState,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Monitors logical IO state and maps logical labels to physical IO card channels.
/// </summary>
public interface IIoStateMonitor : IHardwareInterruptWaiter, IDisposable
{
    event Action<string, bool>? OnIoStateChanged;

    event Action<string, Exception>? OnIoReadFailed;

    int PollingIntervalMs { get; set; }

    void Initialize(
        IEnumerable<IIoCardDevice> devices,
        IEnumerable<IoPoint> diConfigs,
        IEnumerable<IoPoint> doConfigs);

    void Stop();

    bool ReadDi(string label);

    void WriteDo(string label, bool state);

    void WritePulse(string label, int durationMs);

    Dictionary<string, bool> RefreshAllDi();
}

public interface IIoPointRegistry
{
    void SetDoName(int channel, string name);
    IEnumerable<(int Index, string Name)> GetAllOutputs();
    void SetDiName(int channel, string name);
    IEnumerable<(int Index, string Name)> GetAllInputs();
}

public interface IIoCardDevice :
    IDevice,
    IConfigurableDevice,
    IDigitalInputDevice,
    IDigitalOutputDevice,
    IPulseOutputDevice,
    IIoPointRegistry
{
}
