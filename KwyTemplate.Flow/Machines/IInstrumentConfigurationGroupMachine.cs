using Kwy.Device.Abstractions;

namespace KwyTemplate.Flow.Machines;

/// <summary>
/// 支持在应用当前仪表参数时，按机型规则决定
/// 是否还需要同时下发其他关联仪表。
/// </summary>
public interface IInstrumentConfigurationGroupMachine
{
    IReadOnlyList<IConfigurableDevice> SynchronizeInstrumentConfigurationGroup(IConfigurableDevice editedDevice);

    Task CompleteInstrumentConfigurationGroupApplyAsync(
        IReadOnlyList<IConfigurableDevice> groupDevices,
        CancellationToken cancellationToken = default);
}
