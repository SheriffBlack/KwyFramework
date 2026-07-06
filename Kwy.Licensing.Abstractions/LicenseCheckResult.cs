namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Describes the current software license state.
/// </summary>
public sealed record LicenseCheckResult(
    bool Success,
    string ProviderName,
    IReadOnlySet<string> Features,
    DateTimeOffset? ExpiresAt = null,
    string? Message = null,
    Exception? Exception = null)
{
    public static LicenseCheckResult Succeeded(
        string providerName,
        IEnumerable<string>? features = null,
        DateTimeOffset? expiresAt = null,
        string? message = null)
        => new(true, providerName, ToFeatureSet(features), expiresAt, message);

    public static LicenseCheckResult Failed(
        string providerName,
        string message,
        Exception? exception = null)
        => new(false, providerName, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null, message, exception);

    private static IReadOnlySet<string> ToFeatureSet(IEnumerable<string>? features)
        => features is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(features.Where(item => !string.IsNullOrWhiteSpace(item)), StringComparer.OrdinalIgnoreCase);
}
