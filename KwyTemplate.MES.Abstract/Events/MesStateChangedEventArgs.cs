using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.MES.Abstract.Events;

public sealed class MesStateChangedEventArgs : EventArgs
{
    public MesStateChangedEventArgs(MesConnectionState state, string? message = null, Exception? exception = null)
    {
        State = state;
        Message = message;
        Exception = exception;
    }

    public MesConnectionState State { get; }

    public string? Message { get; }

    public Exception? Exception { get; }
}