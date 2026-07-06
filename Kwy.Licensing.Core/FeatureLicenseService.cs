using Kwy.Licensing.Abstractions;

namespace Kwy.Licensing.Core;

/// <summary>
/// Default feature license service that combines registered license providers.
/// </summary>
public sealed class FeatureLicenseService : IFeatureLicenseService
{
    private readonly IReadOnlyList<ILicenseProvider> providers;
    private readonly FeatureLicenseOptions options;
    private readonly SemaphoreSlim checkLock = new(1, 1);
    private LicenseCheckContext? cachedContext;
    private LicenseCheckResult? cachedResult;
    private DateTimeOffset cacheExpiresAt;

    public FeatureLicenseService(
        IEnumerable<ILicenseProvider> providers,
        FeatureLicenseOptions? options = null)
    {
        this.providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        this.options = options ?? new FeatureLicenseOptions();
    }

    public async ValueTask<LicenseCheckResult> CheckAsync(
        LicenseCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var now = DateTimeOffset.UtcNow;
        if (cachedResult != null && cachedContext == context && now < cacheExpiresAt)
        {
            return cachedResult;
        }

        await checkLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (cachedResult != null && cachedContext == context && now < cacheExpiresAt)
            {
                return cachedResult;
            }

            cachedResult = await CheckCoreAsync(context, cancellationToken).ConfigureAwait(false);
            cachedContext = context;
            cacheExpiresAt = now.Add(options.CacheDuration);
            return cachedResult;
        }
        finally
        {
            checkLock.Release();
        }
    }

    public async ValueTask<bool> IsFeatureEnabledAsync(
        string featureCode,
        LicenseCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureCode);

        var result = await CheckAsync(context with { RequestedFeature = featureCode }, cancellationToken).ConfigureAwait(false);
        return result.Success
            && (result.Features.Contains(KwyLicenseFeatures.All) || result.Features.Contains(featureCode));
    }

    private async ValueTask<LicenseCheckResult> CheckCoreAsync(
        LicenseCheckContext context,
        CancellationToken cancellationToken)
    {
        if (providers.Count == 0)
        {
            return LicenseCheckResult.Failed("Kwy.Licensing", "No license provider was registered.");
        }

        var results = new List<LicenseCheckResult>(providers.Count);
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await provider.CheckAsync(context, cancellationToken).ConfigureAwait(false);
                results.Add(result);
                if (options.AllowAnyProvider && result.Success)
                {
                    return result;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(LicenseCheckResult.Failed(
                    provider.ProviderName,
                    $"License provider {provider.ProviderName} failed: {ex.Message}",
                    ex));
            }
        }

        var successful = results.Where(result => result.Success).ToArray();
        if (successful.Length > 0)
        {
            var features = successful.SelectMany(result => result.Features).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expiresAt = successful
                .Where(result => result.ExpiresAt.HasValue)
                .Select(result => result.ExpiresAt!.Value)
                .DefaultIfEmpty()
                .Min();
            return LicenseCheckResult.Succeeded(
                "Composite",
                features,
                expiresAt == default ? null : expiresAt,
                "License granted by multiple providers.");
        }

        string message = string.Join("; ", results.Select(result => $"{result.ProviderName}: {result.Message}"));
        return LicenseCheckResult.Failed("Composite", string.IsNullOrWhiteSpace(message) ? "License check failed." : message);
    }
}
