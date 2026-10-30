using Kwy.Device.Motion.Core;
using Kwy.Device.Motion.Abstractions;
using Kwy.Device.Motion.Abstractions.Axes;
using Kwy.Device.Motion.Core.Axes;
using Kwy.Device.Motion.Core.Safety;

namespace Kwy.Device.Motion.Core;

/// <summary>
/// 统一装配一张物理运动卡的 Core 运行时。
/// 厂商项目只创建设备与状态监视配置，不重复组装监视器、准入守卫、执行器和回零生命周期。
/// </summary>
public static class MotionRuntimeFactory
{
    public static IMotionDeviceRuntime Create(
        IMotionController card,
        MotionStateMonitorOptions stateMonitorOptions,
        MotionAdmissionOptions? admissionOptions = null,
        IAxisHomeLifecycle? homeLifecycle = null,
        IAxisBrakeCoordinator? brakeCoordinator = null,
        IMotionStateSource? stateSource = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(stateMonitorOptions);
        stateMonitorOptions.Validate();
        admissionOptions ??= new MotionAdmissionOptions();

        if (card is not IAxisMotionController controller
            || card is not IMotionProfileController profileController)
        {
            throw new NotSupportedException($"Motion controller '{card.DeviceId}' does not provide the physical single-axis capabilities required by the Core runtime.");
        }

        MotionStateMonitor monitor;
        if (stateSource is not null)
        {
            monitor = new MotionStateMonitor(stateSource, stateMonitorOptions);
        }
        else if (card is IAxisSnapshotReader snapshotReader)
        {
            monitor = new MotionStateMonitor(snapshotReader, stateMonitorOptions);
        }
        else
        {
            throw new NotSupportedException(
                $"Motion controller '{card.DeviceId}' must provide IAxisSnapshotReader or an explicit IMotionStateSource.");
        }
        var admission = new MotionAdmissionGuard(card, monitor, admissionOptions, homeLifecycle);
        var executor = new AxisMotionExecutor(controller, profileController, monitor, admission, brakeCoordinator);
        return new MotionDeviceRuntime(card, monitor, executor, homeLifecycle);
    }
}
