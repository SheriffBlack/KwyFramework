using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 自动模式的运动就绪门禁：除静态配置外，还要求控制器已连接、状态监视器已取得首帧。
/// 这不是实时安全保护，只防止设备在明显未就绪时进入自动运行。
/// </summary>
public sealed class MotionAutoModeGate(
    IMotionConfigurationValidator validator,
    IMotionRuntimeRegistry runtimes) : IMotionAutoModeGate
{
    public void EnsureReadyForAutoMode()
    {
        MotionConfigurationValidationResult configuration = validator.Validate();
        var issues = configuration.Issues.ToList();

        foreach (IMotionDeviceRuntime runtime in runtimes.Runtimes)
        {
            if (!runtime.Card.IsConnected)
            {
                issues.Add(new MotionConfigurationIssue(
                    MotionConfigurationIssueSeverity.Error,
                    "MotionControllerOffline",
                    $"Motion controller '{runtime.DeviceId}' is not connected."));
            }

            if (!runtime.StateMonitor.IsRunning)
            {
                issues.Add(new MotionConfigurationIssue(
                    MotionConfigurationIssueSeverity.Error,
                    "MotionStateMonitorStopped",
                    $"Motion state monitor for '{runtime.DeviceId}' is not running."));
            }
        }

        MotionConfigurationValidationResult result = new(issues);
        if (!result.IsValid)
        {
            throw new MotionConfigurationException(result);
        }
    }
}
