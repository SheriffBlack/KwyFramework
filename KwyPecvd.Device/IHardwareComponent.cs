using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KwyPecvd.Device;

/// <summary>
/// 生命周期操作
/// </summary>
public interface IHardwareComponent
{
    string Id { get; }

    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 由RT设备循环周期调用。
    /// 推进斜坡、写入输出、读取反馈并刷新状态。
    /// </summary>
    Task ExecuteCycleAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MFC 复位不能擅自改流量，先只清除软件状态
    /// </summary>
    /// <returns></returns>
    Task ResetAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结束软件对该硬件组件的管理
    /// </summary>
    /// <returns></returns>
    Task ShutdownAsync(
        CancellationToken cancellationToken = default);
}
