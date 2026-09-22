using Kwy.Device.Abstractions.Motion;

namespace Kwy.Device.Core.Motion;

public sealed class MotionAutoModeGate(IMotionConfigurationValidator validator) : IMotionAutoModeGate
{
    public void EnsureReadyForAutoMode() => validator.ValidateAndThrow();
}
