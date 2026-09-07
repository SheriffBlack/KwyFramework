using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

public interface IMachineRuntimeFeature : IDisposable
{
    /// <summary>
    /// 退出时的停止顺序；数值越小越先停止。
    /// </summary>
    int ShutdownOrder => 0;

    bool CanAttach(MachineBase machine);

    void Start(MachineBase machine);

    void Stop();
}
