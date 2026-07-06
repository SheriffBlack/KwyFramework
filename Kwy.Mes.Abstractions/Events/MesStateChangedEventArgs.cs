using Kwy.Mes.Abstractions.Enums;

namespace Kwy.Mes.Abstractions.Events;

public sealed class MesStateChangedEventArgs : EventArgs
{
    public MesStateChangedEventArgs(MesOnlineState oldState, MesOnlineState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public MesOnlineState OldState { get; }

    public MesOnlineState NewState { get; }
}
