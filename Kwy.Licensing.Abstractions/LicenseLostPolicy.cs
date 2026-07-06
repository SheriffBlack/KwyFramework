namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Defines what the application should do when a runtime license provider becomes unavailable.
/// </summary>
public enum LicenseLostPolicy
{
    WarnOnly = 0,
    DisableNewOperations = 1,
    StopEquipmentSafely = 2,
    ShutdownApplication = 3
}
