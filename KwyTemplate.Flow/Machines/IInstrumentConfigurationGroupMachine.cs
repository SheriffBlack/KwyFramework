using Kwy.Device.Abstractions;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// Allows a machine to keep instruments in one process group consistent when
/// SetView applies the configuration of a single member.
/// </summary>
public interface IInstrumentConfigurationGroupMachine
{
    IReadOnlyList<IConfigurableDevice> SynchronizeInstrumentConfigurationGroup(IConfigurableDevice editedDevice);

    Task CompleteInstrumentConfigurationGroupApplyAsync(
        IReadOnlyList<IConfigurableDevice> groupDevices,
        CancellationToken cancellationToken = default);
}
