namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Runs all registered license activators during application startup.
/// </summary>
public interface ILicenseActivationService
{
    /// <summary>
    /// Activates all registered providers and returns one result per provider.
    /// </summary>
    ValueTask<IReadOnlyList<LicenseActivationResult>> ActivateAllAsync(CancellationToken cancellationToken = default);
}
