namespace Kwy.Device.Abstractions.Equipment;

public enum EquipmentControlMode
{
    Unknown,
    Local,
    Remote
}

public enum EquipmentOperationMode
{
    Unknown,
    Manual,
    Auto,
    DryRun,
    Maintenance,
    Engineering,
    Production
}

public sealed record EquipmentMode(
    EquipmentControlMode ControlMode,
    EquipmentOperationMode OperationMode)
{
    public static EquipmentMode Unknown { get; } = new(EquipmentControlMode.Unknown, EquipmentOperationMode.Unknown);
}

public sealed record EquipmentModeChangedEventArgs(
    EquipmentMode PreviousMode,
    EquipmentMode CurrentMode,
    string? Reason = null);
