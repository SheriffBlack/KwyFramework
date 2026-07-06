namespace Kwy.Device.PLCs.Hsl.Licensing;

/// <summary>
/// Options for activating the HslCommunication runtime license.
/// </summary>
public sealed class HslCommunicationLicenseOptions
{
    /// <summary>
    /// Gets or sets the HslCommunication license key or authorization code.
    /// </summary>
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether missing or failed activation should be treated as startup failure.
    /// </summary>
    public bool Required { get; set; } = true;
}
