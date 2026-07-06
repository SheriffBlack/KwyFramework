using System.ComponentModel;

namespace Kwy.Device.Abstractions.PLC;

/// <summary>
/// 全局 PLC 服务接口，用于跨视图/跨模块共享 PLC 状态和实例
/// </summary>
public interface IPlcService : INotifyPropertyChanged
{
    /// <summary>
    /// 主 PLC 物理设备实例
    /// </summary>
    IPlcDevice? MainPlc { get; set; }

    /// <summary>
    /// PLC 实时连接状态 (支持 UI 绑定)
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 刷新/心跳状态 (可选)
    /// </summary>
    void RefreshStatus();
}