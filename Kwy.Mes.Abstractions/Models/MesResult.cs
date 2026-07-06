namespace Kwy.Mes.Abstractions.Models;

public record MesResult
{
    public static MesResult Ok(string message = "OK")
        => new() { Succeeded = true, Code = "OK", Message = message };

    public static MesResult Fail(string code, string message, string? detail = null)
        => new()
        {
            Succeeded = false,
            Code = code,
            Message = message,
            Error = new MesError(code, message, detail)
        };

    public static MesResult Unsupported(string operation)
        => Fail("UNSUPPORTED", $"MES operation is not supported: {operation}.");

    public bool Succeeded { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public MesError? Error { get; init; }

    public string? CorrelationId { get; init; }
}

public sealed record MesResult<T> : MesResult
{
    public static MesResult<T> Ok(T data, string message = "OK")
        => new() { Succeeded = true, Code = "OK", Message = message, Data = data };

    public new static MesResult<T> Fail(string code, string message, string? detail = null)
        => new()
        {
            Succeeded = false,
            Code = code,
            Message = message,
            Error = new MesError(code, message, detail)
        };

    public new static MesResult<T> Unsupported(string operation)
        => Fail("UNSUPPORTED", $"MES operation is not supported: {operation}.");

    public T? Data { get; init; }
}
