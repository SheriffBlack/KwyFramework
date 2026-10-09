using Kwy.Device.Abstractions;

namespace Kwy.Device.IoCard.Abstractions;

/// <summary>模拟量 IO 公共通道约定。</summary>
public static class AnalogIoChannelLimits
{
    /// <summary>未显式声明时的默认通道数，也是当前公共模型支持的最大通道数。</summary>
    public const int DefaultChannelCount = 64;
}

/// <summary>物理模拟输入能力；参数为物理通道，返回设备适配器定义的原始量。</summary>
public interface IAnalogInputDevice : IDevice
{
    /// <summary>设备实际 AI 通道数；未覆盖时默认为 64。</summary>
    int AnalogInputCount => AnalogIoChannelLimits.DefaultChannelCount;
    ValueTask<double> ReadAnalogInputRawAsync(int channel, CancellationToken cancellationToken = default);
}

/// <summary>由硬件适配器上报的原始模拟输入样本。</summary>
public readonly record struct AnalogInputRawSample(
    double RawValue,
    DateTimeOffset Timestamp,
    AnalogIoQuality Quality = AnalogIoQuality.Good);

/// <summary>可选的带硬件质量状态采集能力；支持断线检测的适配器应实现此接口。</summary>
public interface IAnalogInputSampleDevice : IAnalogInputDevice
{
    ValueTask<AnalogInputRawSample> ReadAnalogInputSampleAsync(
        int channel,
        CancellationToken cancellationToken = default);
}

/// <summary>物理模拟输出能力；参数为物理通道和原始量。</summary>
public interface IAnalogOutputDevice : IDevice
{
    /// <summary>设备实际 AO 通道数；未覆盖时默认为 64。</summary>
    int AnalogOutputCount => AnalogIoChannelLimits.DefaultChannelCount;
    ValueTask WriteAnalogOutputRawAsync(int channel, double rawValue, CancellationToken cancellationToken = default);
}

/// <summary>同时具备 AI 和 AO 能力的复合模拟量 IO 设备。</summary>
public interface IAnalogIoDevice : IAnalogInputDevice, IAnalogOutputDevice
{
}

/// <summary>按稳定点位 ID 读取工程量。</summary>
public interface ILogicalAnalogInputReader
{
    ValueTask<double> ReadAsync(string pointId, CancellationToken cancellationToken = default);
    ValueTask<AnalogIoSample> ReadSampleAsync(string pointId, CancellationToken cancellationToken = default);
}

/// <summary>按稳定点位 ID 写入工程量。</summary>
public interface ILogicalAnalogOutputWriter
{
    ValueTask WriteAsync(string pointId, double value, string? owner = null, CancellationToken cancellationToken = default);
}

/// <summary>将已配置安全值的模拟输出收敛到工艺安全状态。</summary>
public interface IAnalogOutputSafeStateController
{
    ValueTask ApplyProcessSafeOutputsAsync(CancellationToken cancellationToken = default);
}
