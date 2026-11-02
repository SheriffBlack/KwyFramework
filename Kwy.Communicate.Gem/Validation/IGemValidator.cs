namespace Kwy.Communicate.Gem;

public interface IGemValidator<in T>
{
    GemValidationResult Validate(T value);
}
