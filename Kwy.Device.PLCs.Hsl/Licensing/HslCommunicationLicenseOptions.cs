namespace Kwy.Device.PLCs.Hsl.Licensing;

/// <summary>
/// Options for activating the HslCommunication runtime license.
/// </summary>
public sealed class HslCommunicationLicenseOptions
{
    /// <summary>
    /// The default HslCommunication authorization code bundled with this driver package.
    /// </summary>
    public const string DefaultLicenseKey = "e0397905-7455-4533-8c7a-3ec89b68b2a7";

    /// <summary>
    /// Gets or sets the HslCommunication license key or authorization code.
    /// </summary>
    public string? LicenseKey { get; set; } = DefaultLicenseKey;

    /// <summary>
    /// Gets or sets a value indicating whether missing or failed activation should be treated as startup failure.
    /// </summary>
    public bool Required { get; set; } = true;
}
