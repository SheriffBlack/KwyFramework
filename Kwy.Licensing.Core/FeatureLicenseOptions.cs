using Kwy.Licensing.Abstractions;

namespace Kwy.Licensing.Core;

/// <summary>
/// Options for feature license checks.
/// </summary>
public sealed class FeatureLicenseOptions
{
    /// <summary>
    /// Gets or sets how long a successful check can be reused.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the policy that the application should apply when a license disappears at runtime.
    /// </summary>
    public LicenseLostPolicy LicenseLostPolicy { get; set; } = LicenseLostPolicy.DisableNewOperations;

    /// <summary>
    /// Gets or sets a value indicating whether any successful provider is enough.
    /// </summary>
    public bool AllowAnyProvider { get; set; } = true;
}
