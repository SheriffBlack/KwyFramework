namespace Kwy.Communicate.Abstractions;

/// <summary>
/// Detailed protocol configuration validation result.
/// </summary>
public sealed record ConfigurationValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
    public static ConfigurationValidationResult Success { get; } = new(Array.Empty<string>());
}
