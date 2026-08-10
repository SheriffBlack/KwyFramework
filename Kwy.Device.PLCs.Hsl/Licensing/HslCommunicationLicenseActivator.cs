using HslCommunication;
using Kwy.Licensing.Abstractions;

namespace Kwy.Device.PLCs.Hsl.Licensing;

/// <summary>
/// Activates the process-wide HslCommunication license once.
/// </summary>
public sealed class HslCommunicationLicenseActivator : ILicenseActivator
{
    private static readonly SemaphoreSlim ActivationLock = new(1, 1);
    private static LicenseActivationResult? cachedResult;

    private readonly HslCommunicationLicenseOptions options;

    public HslCommunicationLicenseActivator(HslCommunicationLicenseOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string ProviderName => "HslCommunication";

    /// <summary>
    /// Activates HslCommunication by using the bundled default authorization code.
    /// </summary>
    public static LicenseActivationResult ActivateDefault()
        => Activate(HslCommunicationLicenseOptions.DefaultLicenseKey, required: true);

    public ValueTask<LicenseActivationResult> ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Activate(options.LicenseKey, options.Required));
    }

    private static LicenseActivationResult Activate(string? licenseKey, bool required)
    {
        if (cachedResult?.Success == true)
        {
            return cachedResult;
        }

        ActivationLock.Wait();
        try
        {
            if (cachedResult?.Success == true)
            {
                return cachedResult;
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                cachedResult = required
                    ? LicenseActivationResult.Failed("HslCommunication", "HslCommunication license key is required but was not configured.")
                    : LicenseActivationResult.Succeeded("HslCommunication", "HslCommunication license activation skipped because no key was configured.");
                return cachedResult;
            }

            try
            {
                bool success = Authorization.SetAuthorizationCode(licenseKey);
                cachedResult = success
                    ? LicenseActivationResult.Succeeded("HslCommunication", "HslCommunication license activated.")
                    : LicenseActivationResult.Failed("HslCommunication", "HslCommunication license activation returned false.");
                return cachedResult;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                cachedResult = LicenseActivationResult.Failed("HslCommunication", $"HslCommunication license activation failed: {ex.Message}", ex);
                return cachedResult;
            }
        }
        finally
        {
            ActivationLock.Release();
        }
    }
}
