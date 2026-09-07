using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Semiconductor.Gem;

public interface IGemEquipmentBridge : IEquipmentEventSink, IDisposable
{
    IReadOnlyCollection<EquipmentEvent> PublishedEvents { get; }

    Task ReportStateChangedAsync(
        EquipmentStateChangedEventArgs args,
        CancellationToken cancellationToken = default);
}
