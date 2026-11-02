namespace Kwy.Communicate.Abstractions;

/// <summary>
/// 详细的协议配置验证结果。
/// </summary>
public sealed record ConfigValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
    public static ConfigValidationResult Success { get; } = new(Array.Empty<string>());
}
