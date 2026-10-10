using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KwyPecvd.Device;

public sealed record CommandResult(
    bool Succeeded,
    string Code,
    string Message)
{
    public static CommandResult Success(
        string message = "Accepted")
        => new(true, ResultCodes.Ok, message);

    public static CommandResult Failure(
        string code,
        string message)
        => new(false, code, message);
}

public static class ResultCodes
{
    public const string Ok = "OK";
    public const string InvalidState = "INVALID_STATE";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string NotReady = "NOT_READY";
    public const string Offline = "OFFLINE";
    public const string Interlock = "INTERLOCK";
    public const string Timeout = "TIMEOUT";
    public const string CommunicationError = "COMMUNICATION_ERROR";
    public const string InternalError = "INTERNAL_ERROR";
}
