using Kwy.Device.Abstractions;
using Kwy.Device.IoCard.Abstractions;

namespace Kwy.Device.IoCard.Core;

/// <summary>将稳定点位 ID 解析为模拟量设备与物理通道，并统一执行换算、量程、Owner 和安全值校验。</summary>
public sealed class LogicalAnalogIoService :
    ILogicalAnalogInputReader,
    ILogicalAnalogOutputWriter,
    IAnalogOutputSafeStateController
{
    private readonly IAnalogIoPointDefinitionProvider definitions;
    private readonly IDeviceRegistry devices;

    public LogicalAnalogIoService(IAnalogIoPointDefinitionProvider definitions, IDeviceRegistry devices)
    {
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
    }

    public async ValueTask<double> ReadAsync(string pointId, CancellationToken cancellationToken = default)
        => (await ReadSampleAsync(pointId, cancellationToken).ConfigureAwait(false)).Value;

    public async ValueTask<AnalogIoSample> ReadSampleAsync(string pointId, CancellationToken cancellationToken = default)
    {
        AnalogIoPointDefinition point = GetPoint(pointId, AnalogIoDirection.Input);
        IAnalogInputDevice device = devices.GetRequiredDevice<IAnalogInputDevice>(point.DeviceId);
        ValidateChannel(point, device.AnalogInputCount);

        AnalogInputRawSample rawSample = device is IAnalogInputSampleDevice sampleDevice
            ? await sampleDevice.ReadAnalogInputSampleAsync(point.Channel, cancellationToken).ConfigureAwait(false)
            : new(
                await device.ReadAnalogInputRawAsync(point.Channel, cancellationToken).ConfigureAwait(false),
                DateTimeOffset.UtcNow);
        double rawValue = rawSample.RawValue;
        AnalogIoQuality quality = rawSample.Quality == AnalogIoQuality.Good
            ? GetQuality(point, rawValue)
            : rawSample.Quality;
        double value = double.IsFinite(rawValue) ? point.ToEngineeringValue(rawValue) : double.NaN;
        return new(point.Id, value, rawValue, rawSample.Timestamp, quality);
    }

    public async ValueTask WriteAsync(
        string pointId,
        double value,
        string? owner = null,
        CancellationToken cancellationToken = default)
    {
        AnalogIoPointDefinition point = GetPoint(pointId, AnalogIoDirection.Output);
        ValidateOwner(point, owner);
        ValidateOutputValue(point, value);

        IAnalogOutputDevice device = devices.GetRequiredDevice<IAnalogOutputDevice>(point.DeviceId);
        ValidateChannel(point, device.AnalogOutputCount);
        await device.WriteAnalogOutputRawAsync(point.Channel, point.ToRawValue(value), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ApplyProcessSafeOutputsAsync(CancellationToken cancellationToken = default)
    {
        var failures = new List<Exception>();
        foreach (AnalogIoPointDefinition point in definitions.GetByDirection(AnalogIoDirection.Output))
        {
            if (point.ProcessSafeValue is not { } safeValue) continue;
            try
            {
                await WriteAsync(point.Id, safeValue, point.Owner, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new InvalidOperationException($"模拟输出“{point.Id}”写入工艺安全值失败。", exception));
            }
        }

        if (failures.Count > 0) throw new AggregateException("一个或多个模拟输出未能进入工艺安全状态。", failures);
    }

    private AnalogIoPointDefinition GetPoint(string pointId, AnalogIoDirection expectedDirection)
    {
        AnalogIoPointDefinition point = definitions.GetRequired(pointId);
        if (point.Direction != expectedDirection)
            throw new InvalidOperationException($"模拟量 IO 点位“{point.Id}”为 {point.Direction}，不能用作 {expectedDirection}。");
        return point;
    }

    private static void ValidateOwner(AnalogIoPointDefinition point, string? owner)
    {
        if (point.Owner is not null && !string.Equals(point.Owner, owner, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"模拟输出“{point.Id}”只允许 Owner“{point.Owner}”写入。");
    }

    private static void ValidateOutputValue(AnalogIoPointDefinition point, double value)
    {
        if (!double.IsFinite(value) || value < point.EngineeringMinimum || value > point.EngineeringMaximum)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"模拟输出“{point.Id}”必须位于 [{point.EngineeringMinimum}, {point.EngineeringMaximum}] {point.Unit}。");
        if (point.MinimumAllowedValue is { } min && value < min)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"模拟输出“{point.Id}”低于允许下限 {min}。");
        if (point.MaximumAllowedValue is { } max && value > max)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"模拟输出“{point.Id}”高于允许上限 {max}。");
    }

    private static void ValidateChannel(AnalogIoPointDefinition point, int channelCount)
    {
        if (channelCount is < 1 or > AnalogIoChannelLimits.DefaultChannelCount)
            throw new InvalidOperationException($"设备“{point.DeviceId}”上报了无效的模拟量通道数 {channelCount}，有效范围为 1～64。");
        if (point.Channel >= channelCount)
            throw new ArgumentOutOfRangeException(nameof(point.Channel), point.Channel, $"模拟量点位“{point.Id}”超出设备“{point.DeviceId}”的通道范围。");
    }

    private static AnalogIoQuality GetQuality(AnalogIoPointDefinition point, double rawValue)
    {
        if (!double.IsFinite(rawValue)) return AnalogIoQuality.Invalid;
        if (rawValue < point.RawMinimum) return AnalogIoQuality.UnderRange;
        if (rawValue > point.RawMaximum) return AnalogIoQuality.OverRange;
        return AnalogIoQuality.Good;
    }
}
