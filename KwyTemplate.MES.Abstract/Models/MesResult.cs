namespace KwyTemplate.MES.Abstract.Models;

public record MesResult
{
    public MesResult(bool isSuccess, string code, string message, string? detail = null, MesExchangeRecord? exchange = null)
    {
        IsSuccess = isSuccess;
        Code = string.IsNullOrWhiteSpace(code) ? (isSuccess ? MesResultCodes.Ok : MesResultCodes.Error) : code;
        Message = string.IsNullOrWhiteSpace(message) ? Code : message;
        Detail = detail;
        Exchange = exchange;
    }

    public bool IsSuccess { get; }

    public string Code { get; }

    public string Message { get; }

    public string? Detail { get; }

    public MesExchangeRecord? Exchange { get; }

    public static MesResult Ok(string message = "OK", MesExchangeRecord? exchange = null)
        => new(true, MesResultCodes.Ok, message, exchange: exchange);

    public static MesResult Fail(string code, string message, string? detail = null, MesExchangeRecord? exchange = null)
        => new(false, code, message, detail, exchange);

    public static MesResult Unsupported(string operation)
        => Fail(MesResultCodes.Unsupported, $"MES operation is not supported: {operation}.");
}

public sealed record MesResult<T> : MesResult
{
    public MesResult(bool isSuccess, string code, string message, T? data = default, string? detail = null, MesExchangeRecord? exchange = null)
        : base(isSuccess, code, message, detail, exchange)
    {
        Data = data;
    }

    public T? Data { get; }

    public static MesResult<T> Ok(T data, string message = "OK", MesExchangeRecord? exchange = null)
        => new(true, MesResultCodes.Ok, message, data, exchange: exchange);

    public new static MesResult<T> Fail(string code, string message, string? detail = null, MesExchangeRecord? exchange = null)
        => new(false, code, message, default, detail, exchange);

    public new static MesResult<T> Unsupported(string operation)
        => Fail(MesResultCodes.Unsupported, $"MES operation is not supported: {operation}.");
}

public static class MesResultCodes
{
    public const string Ok = "OK";

    public const string Error = "ERROR";

    public const string Unsupported = "UNSUPPORTED";

    public const string Timeout = "TIMEOUT";

    public const string Rejected = "REJECTED";

    public const string DataNotFound = "DATA_NOT_FOUND";

    public const string DataParseFailed = "DATA_PARSE_FAILED";

    public const string DataWriteFailed = "DATA_WRITE_FAILED";
}
