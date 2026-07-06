namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Checks whether software features are currently licensed.
/// </summary>
public interface IFeatureLicenseService
{
    /// <summary>
    /// Checks the whole application license state.
    /// </summary>
    ValueTask<LicenseCheckResult> CheckAsync(
        LicenseCheckContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific feature is licensed.
    /// </summary>
    ValueTask<bool> IsFeatureEnabledAsync(
        string featureCode,
        LicenseCheckContext context,
        CancellationToken cancellationToken = default);
}
