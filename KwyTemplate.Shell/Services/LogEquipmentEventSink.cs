using System.Collections.Concurrent;
using Kwy.Device.Abstractions.Equipment;
using Kwy.Logging.Abstractions;
using Kwy.UI.WPF.Components.Logging;
using KwyTemplate.Contracts.Services;

namespace KwyTemplate.Shell.Services;

public sealed class LogEquipmentEventSink : IEquipmentEventSink
{
    private static readonly TimeSpan DuplicateSuppressWindow = TimeSpan.FromSeconds(3);
    private readonly ConcurrentQueue<EquipmentEvent> events = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastLoggedAt = new(StringComparer.Ordinal);
    private readonly KwyLogService logService;
    private readonly ILogService developerLog;
    private readonly UserVisibleLogFileService userLogFile;
    private readonly StartupProgressService startupProgress;

    public LogEquipmentEventSink(KwyLogService logService, ILogService developerLog, UserVisibleLogFileService userLogFile, StartupProgressService startupProgress)
    {
        this.logService = logService ?? throw new ArgumentNullException(nameof(logService));
        this.developerLog = developerLog ?? throw new ArgumentNullException(nameof(developerLog));
        this.userLogFile = userLogFile ?? throw new ArgumentNullException(nameof(userLogFile));
        this.startupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
    }

    public IReadOnlyCollection<EquipmentEvent> Events => events.ToArray();

    public Task PublishAsync(EquipmentEvent equipmentEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EquipmentEvent timestamped = equipmentEvent with { Timestamp = equipmentEvent.Timestamp ?? DateTimeOffset.Now };
        events.Enqueue(timestamped);
        TryWriteLog(timestamped);
        return Task.CompletedTask;
    }

    private void TryWriteLog(EquipmentEvent equipmentEvent)
    {
        if (!ShouldLog(equipmentEvent, out string level, out string message))
        {
            return;
        }

        if (IsDuplicateSuppressed(equipmentEvent, message))
        {
            return;
        }

        logService.Add(level, message);
        userLogFile.Add(level, message);
        WriteDeveloperLog(equipmentEvent, level, message);
    }

    private bool ShouldLog(EquipmentEvent equipmentEvent, out string level, out string message)
    {
        level = MapLevel(equipmentEvent.Severity);
        message = FormatMessage(equipmentEvent);

        if (equipmentEvent.Code == "DeviceOperation")
        {
            string operationKind = GetProperty(equipmentEvent, "OperationKind");
            bool isSuccess = string.Equals(GetProperty(equipmentEvent, "IsSuccess"), bool.TrueString, StringComparison.OrdinalIgnoreCase);

            if (isSuccess)
            {
                return string.Equals(operationKind, "ParameterWrite", StringComparison.OrdinalIgnoreCase);
            }

            return operationKind is "Read" or "Write" or "ParameterWrite" or "Trigger";
        }

        if (equipmentEvent.Code == "DeviceError")
        {
            return true;
        }

        if (equipmentEvent.Code == "DeviceStateChanged")
        {
            return ShouldLogRuntimeConnectionState(equipmentEvent, out level, out message);
        }

        return false;
    }


    private bool ShouldLogRuntimeConnectionState(EquipmentEvent equipmentEvent, out string level, out string message)
    {
        level = "Info";
        message = FormatMessage(equipmentEvent);

        // Startup connection logs are written by StartupProgressService; runtime reconnect logs are handled here.
        if (!startupProgress.IsCompleted)
        {
            return false;
        }

        string currentState = GetProperty(equipmentEvent, "CurrentState");
        if (!string.Equals(currentState, "Connected", StringComparison.OrdinalIgnoreCase))
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

    private void WriteDeveloperLog(EquipmentEvent equipmentEvent, string level, string message)
    {
        string exceptionText = GetProperty(equipmentEvent, "Exception");
        Exception? exception = string.IsNullOrWhiteSpace(exceptionText)
            ? null
            : new Exception(exceptionText);

        using IDisposable scope = developerLog.BeginScope(new
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
            Endpoint = GetProperty(equipmentEvent, "Endpoint"),
            Protocol = GetProperty(equipmentEvent, "Protocol")
        });

        if (string.Equals(level, "Error", StringComparison.OrdinalIgnoreCase))
        {
            developerLog.Error(message, exception);
            return;
        }

        if (string.Equals(level, "Warn", StringComparison.OrdinalIgnoreCase))
        {
            developerLog.Warning(message);
            return;
        }

        developerLog.Info(message);
    }

    private bool IsDuplicateSuppressed(EquipmentEvent equipmentEvent, string message)
    {
        if (equipmentEvent.Severity < EquipmentEventSeverity.Error)
        {
            return false;
        }

        string key = string.Concat(equipmentEvent.Code, "|", equipmentEvent.Source, "|", message);
        DateTimeOffset now = DateTimeOffset.Now;
        if (lastLoggedAt.TryGetValue(key, out DateTimeOffset last) && now - last < DuplicateSuppressWindow)
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
            EquipmentEventSeverity.Critical or EquipmentEventSeverity.Error => "Error",
            EquipmentEventSeverity.Warning => "Warn",
            _ => "Info"
        };

    private static string GetProperty(EquipmentEvent equipmentEvent, string key)
        => equipmentEvent.Properties != null && equipmentEvent.Properties.TryGetValue(key, out string? value)
            ? value
            : string.Empty;
}
