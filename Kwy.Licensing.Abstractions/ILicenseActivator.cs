namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Represents a third-party SDK or runtime license activator.
/// </summary>
public interface ILicenseActivator
{
    /// <summary>
    /// Gets the provider name, such as HslCommunication, HALCON, or Cimetrix.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Activates or verifies the license.
    /// </summary>
    ValueTask<LicenseActivationResult> ActivateAsync(CancellationToken cancellationToken = default);
}
