namespace Kwy.Licensing.Abstractions;

/// <summary>
/// Default implementation that activates providers sequentially.
/// </summary>
public sealed class LicenseActivationService : ILicenseActivationService
{
    private readonly IReadOnlyList<ILicenseActivator> activators;

    public LicenseActivationService(IEnumerable<ILicenseActivator> activators)
    {
        this.activators = activators?.ToList() ?? throw new ArgumentNullException(nameof(activators));
    }

    public async ValueTask<IReadOnlyList<LicenseActivationResult>> ActivateAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<LicenseActivationResult>(activators.Count);

        foreach (var activator in activators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.Add(await activator.ActivateAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(LicenseActivationResult.Failed(
                    activator.ProviderName,
                    $"License activation failed for {activator.ProviderName}: {ex.Message}",
                    ex));
            }
        }

        return results;
    }
}
