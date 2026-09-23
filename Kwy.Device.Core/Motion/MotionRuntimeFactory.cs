using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

/// <summary>
/// 统一装配一张物理运动卡的 Core 运行时。
/// 厂商项目只创建设备与状态监视配置，不重复组装监视器、准入守卫、执行器和回零生命周期。
/// </summary>
public static class MotionRuntimeFactory
{
    public static IMotionDeviceRuntime Create(
        IMotionCard card,
        MotionStateMonitorOptions stateMonitorOptions,
        MotionAdmissionOptions? admissionOptions = null,
        IAxisHomeLifecycle? homeLifecycle = null,
        IAxisBrakeCoordinator? brakeCoordinator = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(stateMonitorOptions);
        stateMonitorOptions.Validate();
        admissionOptions ??= new MotionAdmissionOptions();

        if (card is not IAxisMotionController controller
            || card is not IMotionProfileController profileController
            || card is not IAxisSnapshotReader snapshotReader)
        {
            throw new NotSupportedException($"Motion card '{card.DeviceId}' does not provide the physical single-axis and state-snapshot capabilities required by the default Core runtime.");
        }

        var monitor = new MotionStateMonitor(snapshotReader, stateMonitorOptions);
        var admission = new MotionAdmissionGuard(card, monitor, admissionOptions, homeLifecycle);
        var executor = new AxisMotionExecutor(controller, profileController, monitor, admission, brakeCoordinator);
        return new MotionDeviceRuntime(card, monitor, executor, homeLifecycle);
    }
}
