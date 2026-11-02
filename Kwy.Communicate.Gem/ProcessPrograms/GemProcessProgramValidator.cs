namespace Kwy.Communicate.Gem;

/// <summary>验证 Process Program 的基本协议数据。</summary>
public sealed class GemProcessProgramValidator : IGemValidator<GemProcessProgram>
{
    public GemValidationResult Validate(GemProcessProgram value)
        => string.IsNullOrWhiteSpace(value.Ppid)
            ? GemValidationResult.Failure("PPID is required.")
            : GemValidationResult.Success;
}
