using Kwy.Communicate.Gem;
using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Semiconductor.Gem;

public interface IEquipmentGemMapper
{
    uint GetStateChangedEventId(EquipmentStateChangedEventArgs args);

    uint GetEquipmentEventId(EquipmentEvent equipmentEvent);

    uint GetAlarmId(EquipmentEvent equipmentEvent);

    GemAlarm ToGemAlarm(EquipmentEvent equipmentEvent);
}
