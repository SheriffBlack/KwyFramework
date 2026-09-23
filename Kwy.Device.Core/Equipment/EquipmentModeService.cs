using Kwy.Device.Abstractions.Equipment;
using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Equipment;

public sealed class EquipmentModeService : IEquipmentModeService
{
    private readonly SemaphoreSlim modeSemaphore = new(1, 1);
    private readonly IReadOnlyList<IMotionAutoModeGate> motionAutoModeGates;

    /// <summary>
    /// 模式服务不拥有运动配置；通过可选门禁在进入会执行自动动作的模式前统一校验。
    /// 未注册运动模块时集合为空，基础设备仍可独立使用此服务。
    /// </summary>
    public EquipmentModeService(IEnumerable<IMotionAutoModeGate> motionAutoModeGates)
    {
        this.motionAutoModeGates = motionAutoModeGates?.ToArray()
            ?? throw new ArgumentNullException(nameof(motionAutoModeGates));
    }

    public EquipmentMode CurrentMode { get; private set; } = EquipmentMode.Unknown;

    public event EventHandler<EquipmentModeChangedEventArgs>? ModeChanged;

    public async Task SetModeAsync(
        EquipmentMode mode,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await modeSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (CurrentMode == mode)
            {
                return;
            }

            if (RequiresMotionAutoModeGate(mode))
            {
                foreach (IMotionAutoModeGate gate in motionAutoModeGates)
                {
                    // 配置错误必须在进入自动模式前暴露，而不是由首个运动动作延后发现。
                    gate.EnsureReadyForAutoMode();
                }
            }

            var previous = CurrentMode;
            CurrentMode = mode;
            ModeChanged?.Invoke(this, new EquipmentModeChangedEventArgs(previous, mode, reason));
        }
        finally
        {
            modeSemaphore.Release();
        }
    }

    private static bool RequiresMotionAutoModeGate(EquipmentMode mode)
        => mode.OperationMode is EquipmentOperationMode.Auto
            or EquipmentOperationMode.DryRun
            or EquipmentOperationMode.Production;
}
