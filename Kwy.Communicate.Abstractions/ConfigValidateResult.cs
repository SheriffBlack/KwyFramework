namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 详细的协议配置验证结果。
/// </summary>
public sealed record ConfigValidateResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
    public static ConfigValidateResult Success { get; } = new(Array.Empty<string>());
}
