namespace Kwy.Communicate.Gem;

public sealed record GemValidationResult(bool IsValid, string? Error = null)
{
    public static GemValidationResult Success { get; } = new(true);

    public static GemValidationResult Failure(string error) => new(false, error);
}
