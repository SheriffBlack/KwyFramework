using Kwy.Communicate.Abstractions.Enums;
using Kwy.Communicate.Abstractions.Events;

namespace Kwy.Device.Abstractions;

/// <summary>
/// 设备核心接口
/// </summary>
public interface IDevice : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 设备唯一标识
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// 设备名称
    /// </summary>
    string DeviceName { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接状态
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// 状态改变事件
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs> StateChanged;

    /// <summary>
    /// 错误发生事件
    /// </summary>
    event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

    /// <summary>
    /// 异步连接设备
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步断开连接
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
