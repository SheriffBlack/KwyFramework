using Kwy.Communicate.Gem;
using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Semiconductor.Gem;

public sealed class DefaultEquipmentGemMapper : IEquipmentGemMapper
{
    private readonly GemEquipmentBridgeOptions options;

    public DefaultEquipmentGemMapper(GemEquipmentBridgeOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public uint GetStateChangedEventId(EquipmentStateChangedEventArgs args)
    {
        return options.StateEventIds.TryGetValue(args.CurrentState, out var ceid)
            ? ceid
            : options.StateChangedCeid;
    }

    public uint GetEquipmentEventId(EquipmentEvent equipmentEvent)
    {
        ArgumentNullException.ThrowIfNull(equipmentEvent);

        return options.EventIds.TryGetValue(equipmentEvent.Code, out var ceid)
            ? ceid
            : CreateStableId(options.EventCeidBase, equipmentEvent.Code, options.GeneratedIdModulo);
    }

    public uint GetAlarmId(EquipmentEvent equipmentEvent)
    {
        ArgumentNullException.ThrowIfNull(equipmentEvent);

        return options.AlarmIds.TryGetValue(equipmentEvent.Code, out var alid)
            ? alid
            : CreateStableId(options.AlarmIdBase, equipmentEvent.Code, options.GeneratedIdModulo);
    }

    public GemAlarm ToGemAlarm(EquipmentEvent equipmentEvent)
    {
        ArgumentNullException.ThrowIfNull(equipmentEvent);

        var state = equipmentEvent.Severity >= EquipmentEventSeverity.Error
            ? GemAlarmState.Set
            : GemAlarmState.Clear;

        return new GemAlarm(
            GetAlarmId(equipmentEvent),
            equipmentEvent.Message,
            state,
            ToAlarmCode(equipmentEvent.Severity));
    }

    private static byte ToAlarmCode(EquipmentEventSeverity severity)
        => severity switch
        {
            EquipmentEventSeverity.Critical => 2,
            EquipmentEventSeverity.Error => 1,
            _ => 0
        };

    private static uint CreateStableId(uint idBase, string code, uint modulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var hash = 2166136261u;
        foreach (var ch in code)
        {
            hash ^= char.ToUpperInvariant(ch);
            hash *= 16777619u;
        }

        var range = Math.Max(1u, modulo);
        return idBase + hash % range;
    }
}
