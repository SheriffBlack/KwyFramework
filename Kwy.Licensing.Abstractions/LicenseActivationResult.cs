namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Describes the outcome of a license activation attempt.
/// </summary>
public sealed record LicenseActivationResult(
    bool Success,
    string ProviderName,
    string? Message = null,
    Exception? Exception = null)
{
    public static LicenseActivationResult Succeeded(string providerName, string? message = null)
        => new(true, providerName, message);

    public static LicenseActivationResult Failed(string providerName, string message, Exception? exception = null)
        => new(false, providerName, message, exception);
}
