using Kwy.Device.Abstractions;

namespace Kwy.Device.Core.Equipment;

/// <summary>将设备实例事件转换为整机事件和报警。</summary>
public interface IDeviceEventForwarder
{
    void Attach(IDevice device);

    void Detach(IDevice device);
}
