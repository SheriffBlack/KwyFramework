using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Equipment;
using Kwy.UI.WPF.Components.ActivityLog;
using KwyTemplate.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace KwyTemplate.Shell.Services;

/// <summary>
/// 将整机事件适配为操作员可见的活动摘要和开发诊断日志。
/// </summary>
public sealed class LogEquipmentEventSink : IEquipmentEventSink
{
    private static readonly TimeSpan DuplicateSuppressWindow =
        TimeSpan.FromSeconds(3);

    private readonly ConcurrentQueue<EquipmentEvent> events = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastLoggedAt =
        new(StringComparer.Ordinal);
    private readonly KwyActivityLogService activityLog;
    private readonly ILogger<LogEquipmentEventSink> developerLog;
    private readonly StartupProgressService startupProgress;

    public LogEquipmentEventSink(
        KwyActivityLogService activityLog,
        ILogger<LogEquipmentEventSink> developerLog,
        StartupProgressService startupProgress)
    {
        this.activityLog = activityLog
            ?? throw new ArgumentNullException(nameof(activityLog));
        this.developerLog = developerLog
            ?? throw new ArgumentNullException(nameof(developerLog));
        this.startupProgress = startupProgress
            ?? throw new ArgumentNullException(nameof(startupProgress));
    }

    public IReadOnlyCollection<EquipmentEvent> Events => events.ToArray();

    public Task PublishAsync(
        EquipmentEvent equipmentEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EquipmentEvent timestamped = equipmentEvent with
        {
            Timestamp = equipmentEvent.Timestamp ?? DateTimeOffset.Now
        };

        events.Enqueue(timestamped);
        TryWriteLog(timestamped);
        return Task.CompletedTask;
    }

    private void TryWriteLog(EquipmentEvent equipmentEvent)
    {
        if (!ShouldLog(equipmentEvent, out string level, out string message)
            || IsDuplicateSuppressed(equipmentEvent, message))
        {
            return;
        }

        activityLog.Add(level, message);
        WriteDeveloperLog(equipmentEvent, level, message);
    }

    private bool ShouldLog(
        EquipmentEvent equipmentEvent,
        out string level,
        out string message)
    {
        level = MapLevel(equipmentEvent.Severity);
        message = FormatMessage(equipmentEvent);

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
                return string.Equals(
                    operationKind,
                    "ParameterWrite",
                    StringComparison.OrdinalIgnoreCase);
            }

            return operationKind is "Read"
                or "Write"
                or "ParameterWrite"
                or "Trigger";
        }

        if (equipmentEvent.Code == DeviceEventCodes.Error)
        {
            return true;
        }

        if (equipmentEvent.Code == DeviceEventCodes.StateChanged)
        {
            return ShouldLogRuntimeConnectionState(
                equipmentEvent,
                out level,
                out message);
        }

        return false;
    }

    private bool ShouldLogRuntimeConnectionState(
        EquipmentEvent equipmentEvent,
        out string level,
        out string message)
    {
        level = "Info";
        message = FormatMessage(equipmentEvent);

        // 启动阶段连接日志由 StartupProgressService 负责，这里只处理运行期重连。
        if (!startupProgress.IsCompleted)
        {
            return false;
        }

        string currentState = GetProperty(
            equipmentEvent,
            "CurrentState");

        if (!string.Equals(
                currentState,
                "Connected",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string deviceName = GetProperty(equipmentEvent, "DeviceName");
        string displayName = string.IsNullOrWhiteSpace(deviceName)
            ? equipmentEvent.Source ?? "Device"
            : deviceName;

        message = $"设备连接成功：{displayName}";
        return true;
    }

    private void WriteDeveloperLog(
        EquipmentEvent equipmentEvent,
        string level,
        string message)
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

        if (string.Equals(
                level,
                "Error",
                StringComparison.OrdinalIgnoreCase))
        {
            developerLog.LogError("{Message}", message);
            return;
        }

        if (string.Equals(
                level,
                "Warn",
                StringComparison.OrdinalIgnoreCase))
        {
            developerLog.LogWarning("{Message}", message);
            return;
        }

        developerLog.LogInformation("{Message}", message);
    }

    private bool IsDuplicateSuppressed(
        EquipmentEvent equipmentEvent,
        string message)
    {
        if (equipmentEvent.Severity < EquipmentEventSeverity.Error)
        {
            return false;
        }

        string key = string.Concat(
            equipmentEvent.Code,
            "|",
            equipmentEvent.Source,
            "|",
            message);
        DateTimeOffset now = DateTimeOffset.Now;

        if (lastLoggedAt.TryGetValue(key, out DateTimeOffset last)
            && now - last < DuplicateSuppressWindow)
        {
            return true;
        }

        lastLoggedAt[key] = now;
        return false;
    }

    private static string FormatMessage(EquipmentEvent equipmentEvent)
    {
        string deviceName = GetProperty(equipmentEvent, "DeviceName");
        string prefix = string.IsNullOrWhiteSpace(deviceName)
            ? equipmentEvent.Source ?? "Device"
            : deviceName;

        return string.IsNullOrWhiteSpace(equipmentEvent.Message)
            ? prefix
            : $"{prefix}: {equipmentEvent.Message}";
    }

    private static string MapLevel(EquipmentEventSeverity severity)
        => severity switch
        {
            EquipmentEventSeverity.Critical or EquipmentEventSeverity.Error
                => "Error",
            EquipmentEventSeverity.Warning => "Warn",
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
