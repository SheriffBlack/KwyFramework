namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Provides software or feature authorization information.
/// </summary>
public interface ILicenseProvider
{
    /// <summary>
    /// Gets the provider name, such as Dongle, LocalFile, or Cloud.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Checks the current license state.
    /// </summary>
    ValueTask<LicenseCheckResult> CheckAsync(
        LicenseCheckContext context,
        CancellationToken cancellationToken = default);
}
