namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Carries information for software or feature license checks.
/// </summary>
public sealed record LicenseCheckContext(
    string ApplicationId,
    string? CustomerId = null,
    string? MachineId = null,
    string? RequestedFeature = null);
