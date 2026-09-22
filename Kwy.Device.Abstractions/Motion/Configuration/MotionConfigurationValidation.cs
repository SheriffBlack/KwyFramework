namespace Kwy.Device.Abstractions.Motion;

/// <summary>运动配置项问题严重等级，校验轴/凸轮/坐标系配置时使用</summary>
public enum MotionConfigurationIssueSeverity
{
    /// <summary>错误：配置非法，禁止加载、禁止启动运动模块</summary>
    Error,

    /// <summary>警告：配置可以加载运行，但存在风险，提醒工程师</summary>
    Warning
}

public sealed record MotionConfigurationIssue(MotionConfigurationIssueSeverity Severity, string Code, string Message);

public sealed record MotionConfigurationValidationResult(IReadOnlyList<MotionConfigurationIssue> Issues)
{
    public bool IsValid => Issues.All(item => item.Severity != MotionConfigurationIssueSeverity.Error);
}

/// <summary>设备连接完成、进入自动模式前执行的集中运动配置校验。</summary>
public interface IMotionConfigurationValidator
{
    MotionConfigurationValidationResult Validate();

    void ValidateAndThrow();
}

/// <summary>进入自动模式前的运动配置硬门禁。</summary>
public interface IMotionAutoModeGate
{
    void EnsureReadyForAutoMode();
}

public sealed class MotionConfigurationException(MotionConfigurationValidationResult result)
    : InvalidOperationException(string.Join("; ", result.Issues.Where(item => item.Severity == MotionConfigurationIssueSeverity.Error).Select(item => item.Message)))
{
    public MotionConfigurationValidationResult Result { get; } = result;
}