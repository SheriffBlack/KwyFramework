using KwyTemplate.Flow.Machines;

namespace KwyTemplate.App.Orchestration;

public interface IMachineRuntimeFeature : IDisposable
{
    bool CanAttach(MachineBase machine);

    void Start(MachineBase machine);

    void Stop();
}