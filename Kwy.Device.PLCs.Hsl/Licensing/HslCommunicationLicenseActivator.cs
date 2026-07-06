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

    public async ValueTask<LicenseActivationResult> ActivateAsync(CancellationToken cancellationToken = default)
    {
        if (cachedResult?.Success == true)
        {
            return cachedResult;
        }

        await ActivationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (cachedResult?.Success == true)
            {
                return cachedResult;
            }

            if (string.IsNullOrWhiteSpace(options.LicenseKey))
            {
                cachedResult = options.Required
                    ? LicenseActivationResult.Failed(ProviderName, "HslCommunication license key is required but was not configured.")
                    : LicenseActivationResult.Succeeded(ProviderName, "HslCommunication license activation skipped because no key was configured.");
                return cachedResult;
            }

            try
            {
                bool success = Authorization.SetAuthorizationCode(options.LicenseKey);
                cachedResult = success
                    ? LicenseActivationResult.Succeeded(ProviderName, "HslCommunication license activated.")
                    : LicenseActivationResult.Failed(ProviderName, "HslCommunication license activation returned false.");
                return cachedResult;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                cachedResult = LicenseActivationResult.Failed(ProviderName, $"HslCommunication license activation failed: {ex.Message}", ex);
                return cachedResult;
            }
        }
        finally
        {
            ActivationLock.Release();
        }
    }
}
