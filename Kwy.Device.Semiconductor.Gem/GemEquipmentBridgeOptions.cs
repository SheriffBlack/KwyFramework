using Kwy.Device.Abstractions.Equipment;

namespace Kwy.Device.Semiconductor.Gem;

public sealed class GemEquipmentBridgeOptions
{
    public bool RegisterAsPrimaryEventSink { get; set; } = true;

    public bool ReportStateChanges { get; set; } = true;

    public bool ReportEquipmentEvents { get; set; } = true;

    public bool ReportAlarms { get; set; } = true;

    public uint StateChangedCeid { get; set; } = 1000;

    public uint StateChangedRptid { get; set; } = 1000;

    public uint EquipmentEventRptid { get; set; } = 2000;

    public uint StateVid { get; set; } = 1001;

    public uint PreviousStateVid { get; set; } = 1002;

    public uint StateReasonVid { get; set; } = 1003;

    public uint EventCodeVid { get; set; } = 2001;

    public uint EventMessageVid { get; set; } = 2002;

    public uint EventKindVid { get; set; } = 2003;

    public uint EventSeverityVid { get; set; } = 2004;

    public uint EventSourceVid { get; set; } = 2005;

    public uint EventCeidBase { get; set; } = 20000;

    public uint AlarmIdBase { get; set; } = 30000;

    public uint GeneratedIdModulo { get; set; } = 100000;

    public Dictionary<string, uint> EventIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, uint> AlarmIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<EquipmentRunState, uint> StateEventIds { get; } = new();
}
