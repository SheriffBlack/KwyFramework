using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Equipment;
using Kwy.UI.WPF.Components.ActivityLog;
using KwyTemplate.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace KwyTemplate.Shell.Services;

/// <summary>
/// 将整机事件分别投影为操作员安全摘要和开发诊断日志。
/// </summary>
public sealed class LogEquipmentEventSink : IEquipmentEventSink
{
    private static readonly TimeSpan DuplicateSuppressWindow =
        TimeSpan.FromSeconds(3);

    private readonly ConcurrentQueue<EquipmentEvent> events = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastLoggedAt =
        new(StringComparer.Ordinal);
    private readonly KwyActivityLogService activityLog;
    private readonly IOperatorActivitySink operatorActivitySink;
    private readonly ILogger<LogEquipmentEventSink> developerLog;
    private readonly StartupProgressService startupProgress;

    public LogEquipmentEventSink(
        KwyActivityLogService activityLog,
        IOperatorActivitySink operatorActivitySink,
        ILogger<LogEquipmentEventSink> developerLog,
        StartupProgressService startupProgress)
    {
        this.activityLog = activityLog
            ?? throw new ArgumentNullException(nameof(activityLog));
        this.operatorActivitySink = operatorActivitySink
            ?? throw new ArgumentNullException(nameof(operatorActivitySink));
        this.developerLog = developerLog
            ?? throw new ArgumentNullException(nameof(developerLog));
        this.startupProgress = startupProgress
            ?? throw new ArgumentNullException(nameof(startupProgress));
    }

    public IReadOnlyCollection<EquipmentEvent> Events => events.ToArray();

    public async Task PublishAsync(
        EquipmentEvent equipmentEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(equipmentEvent);
        cancellationToken.ThrowIfCancellationRequested();

        EquipmentEvent timestamped = equipmentEvent with
        {
            Timestamp = equipmentEvent.Timestamp ?? DateTimeOffset.Now
        };

        events.Enqueue(timestamped);

        if (!TryCreateOperatorEntry(timestamped, out OperatorActivityEntry entry)
            || IsDuplicateSuppressed(entry))
        {
            return;
        }

        activityLog.Add(
            ToActivityLogLevel(entry.Level),
            $"{entry.Source}：{entry.Message}");

        // 原始消息和技术上下文仅进入 Developer 日志。
        WriteDeveloperLog(timestamped);

        // Operator Sink 的输入模型不包含 Exception，只能持久化脱敏摘要。
        await operatorActivitySink
            .WriteAsync(entry, cancellationToken)
            .ConfigureAwait(false);
    }

    private bool TryCreateOperatorEntry(
        EquipmentEvent equipmentEvent,
        out OperatorActivityEntry entry)
    {
        string source = GetDisplaySource(equipmentEvent);
        DateTimeOffset timestamp = equipmentEvent.Timestamp
            ?? DateTimeOffset.Now;

        if (equipmentEvent.Code == DeviceEventCodes.Operation)
        {
            string operationKind = GetProperty(
                equipmentEvent,
                "OperationKind");
            bool isSuccess = string.Equals(
                GetProperty(equipmentEvent, "IsSuccess"),
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);

            if (isSuccess)
            {
                if (!string.Equals(
                        operationKind,
                        "ParameterWrite",
                        StringComparison.OrdinalIgnoreCase))
                {
                    entry = null!;
                    return false;
                }

                entry = new OperatorActivityEntry(
                    timestamp,
                    OperatorActivityLevel.Success,
                    "DEVICE.PARAMETER_WRITE.SUCCEEDED",
                    source,
                    "参数写入成功。");
                return true;
            }

            string? message = operationKind switch
            {
                "Read" => "数据读取失败，请检查设备状态。",
                "Write" => "数据写入失败，请重试或联系技术人员。",
                "ParameterWrite" => "参数写入失败，请重试或联系技术人员。",
                "Trigger" => "设备触发失败，请检查设备状态。",
                _ => null
            };

            if (message == null)
            {
                entry = null!;
                return false;
            }

            entry = new OperatorActivityEntry(
                timestamp,
                OperatorActivityLevel.Error,
                $"DEVICE.{operationKind.ToUpperInvariant()}.FAILED",
                source,
                message);
            return true;
        }

        if (equipmentEvent.Code == DeviceEventCodes.Error)
        {
            entry = new OperatorActivityEntry(
                timestamp,
                OperatorActivityLevel.Error,
                "DEVICE.ERROR",
                source,
                "设备发生异常，请联系技术人员。");
            return true;
        }

        if (equipmentEvent.Code == DeviceEventCodes.StateChanged
            && startupProgress.IsCompleted
            && string.Equals(
                GetProperty(equipmentEvent, "CurrentState"),
                "Connected",
                StringComparison.OrdinalIgnoreCase))
        {
            entry = new OperatorActivityEntry(
                timestamp,
                OperatorActivityLevel.Information,
                "DEVICE.CONNECTED",
                source,
                "设备连接成功。");
            return true;
        }

        entry = null!;
        return false;
    }

    private void WriteDeveloperLog(EquipmentEvent equipmentEvent)
    {
        using IDisposable? scope = developerLog.BeginScope(new
        {
            equipmentEvent.Code,
            equipmentEvent.Kind,
            equipmentEvent.Source,
            Severity = equipmentEvent.Severity.ToString(),
            DeviceName = GetProperty(equipmentEvent, "DeviceName"),
            DeviceType = GetProperty(equipmentEvent, "DeviceType"),
            OperationKind = GetProperty(equipmentEvent, "OperationKind"),
            OperationName = GetProperty(equipmentEvent, "OperationName"),
            ExceptionType = GetProperty(equipmentEvent, "ExceptionType"),
            ExceptionMessage = GetProperty(equipmentEvent, "Exception"),
            Endpoint = GetProperty(equipmentEvent, "Endpoint"),
            Protocol = GetProperty(equipmentEvent, "Protocol")
        });

        string message = FormatDeveloperMessage(equipmentEvent);
        switch (equipmentEvent.Severity)
        {
            case EquipmentEventSeverity.Critical:
            case EquipmentEventSeverity.Error:
                developerLog.LogError("{Message}", message);
                break;

            case EquipmentEventSeverity.Warning:
                developerLog.LogWarning("{Message}", message);
                break;

            default:
                developerLog.LogInformation("{Message}", message);
                break;
        }
    }

    private bool IsDuplicateSuppressed(OperatorActivityEntry entry)
    {
        if (entry.Level != OperatorActivityLevel.Error)
        {
            return false;
        }

        string key = string.Concat(
            entry.EventCode,
            "|",
            entry.Source,
            "|",
            entry.Message);
        DateTimeOffset now = DateTimeOffset.Now;

        if (lastLoggedAt.TryGetValue(key, out DateTimeOffset last)
            && now - last < DuplicateSuppressWindow)
        {
            return true;
        }

        lastLoggedAt[key] = now;
        return false;
    }

    private static string GetDisplaySource(EquipmentEvent equipmentEvent)
    {
        string deviceName = GetProperty(equipmentEvent, "DeviceName");
        return string.IsNullOrWhiteSpace(deviceName)
            ? equipmentEvent.Source ?? "设备"
            : deviceName;
    }

    private static string FormatDeveloperMessage(EquipmentEvent equipmentEvent)
    {
        string source = equipmentEvent.Source ?? "Device";
        return string.IsNullOrWhiteSpace(equipmentEvent.Message)
            ? source
            : $"{source}: {equipmentEvent.Message}";
    }

    private static string ToActivityLogLevel(OperatorActivityLevel level)
        => level switch
        {
            OperatorActivityLevel.Success => "Success",
            OperatorActivityLevel.Warning => "Warn",
            OperatorActivityLevel.Error => "Error",
            _ => "Info"
        };

    private static string GetProperty(
        EquipmentEvent equipmentEvent,
        string key)
        => equipmentEvent.Properties != null
            && equipmentEvent.Properties.TryGetValue(key, out string? value)
                ? value
                : string.Empty;
}
