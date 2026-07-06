using Kwy.Communicate.Abstractions.Events;
using Kwy.Device.Abstractions.PLC;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kwy.Device.Core.PLC;

/// <summary>
/// 全局 PLC 服务实现类 (通常作为单例注入到依赖注入容器中)
/// 负责跨模块共享 PLC 状态，并与 UI 进行双向数据绑定
/// </summary>
public class PlcService : IPlcService
{
    private IPlcDevice? _mainPlc;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 设置或获取主 PLC 实例
    /// 自动处理新旧实例的事件订阅/反订阅
    /// </summary>
    public IPlcDevice? MainPlc
    {
        get => _mainPlc;
        set
        {
            if (_mainPlc == value) return;

            // 取消旧实例的订阅，防止内存泄漏
            if (_mainPlc != null)
            {
                _mainPlc.StateChanged -= OnPlcStateChanged;
            }

            _mainPlc = value;

            // 订阅新实例的连接状态变化，以便自动通知 UI
            if (_mainPlc != null)
            {
                _mainPlc.StateChanged += OnPlcStateChanged;
            }

            OnPropertyChanged();
            // 主设备更换时，连接状态也可能改变了
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    /// <summary>
    /// 获取实时物理连接状态 (供 UI 绑定使用)
    /// </summary>
    public bool IsConnected => MainPlc?.IsConnected ?? false;

    /// <summary>
    /// 手动刷新状态通知 (用于 UI 强制重绘)
    /// </summary>
    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(IsConnected));
    }

    /// <summary>
    /// 当底层的 PLC 设备状态发生物理改变时触发
    /// </summary>
    private void OnPlcStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        // 向上层抛出 PropertyChanged 事件，触发 UI (如呼吸灯、按钮状态) 自动刷新
        OnPropertyChanged(nameof(IsConnected));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
