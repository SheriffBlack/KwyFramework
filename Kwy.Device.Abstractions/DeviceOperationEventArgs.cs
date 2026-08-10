namespace Kwy.Device.Abstractions;

public enum DeviceOperationKind
{
    Read,
    Write,
    ParameterWrite,
    Trigger,
    Correction
}

public sealed class DeviceOperationEventArgs : EventArgs
{
    public DeviceOperationEventArgs(
        DeviceOperationKind kind,
        string operationName,
        bool isSuccess,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        Kind = kind;
        OperationName = string.IsNullOrWhiteSpace(operationName) ? kind.ToString() : operationName;
        IsSuccess = isSuccess;
        Message = message ?? string.Empty;
        Exception = exception;
        Properties = properties;
    }

    public DeviceOperationKind Kind { get; }

    public string OperationName { get; }

    public bool IsSuccess { get; }

    public string Message { get; }

    public Exception? Exception { get; }

    public IReadOnlyDictionary<string, string>? Properties { get; }
}