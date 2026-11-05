namespace Kwy.Communicate.Gem;

/// <summary>GEM 会话状态发生变化时提供不可变快照。</summary>
public sealed record GemSessionStateChangedEventArgs(
    GemCommunicationState PreviousCommunicationState,
    GemCommunicationState CommunicationState,
    GemControlState PreviousControlState,
    GemControlState ControlState,
    string Reason);

/// <summary>
/// 集中维护 Equipment 侧 GEM Communication State 与 Control State。
/// </summary>
/// <remarks>
/// 本类型只维护协议状态，不负责 HSMS 收发、设备工艺状态或安全互锁。
/// 调用方应先完成业务授权，再提交相应状态转换。
/// </remarks>
public sealed class GemSessionStateMachine
{
    private readonly object syncRoot = new();
    private GemCommunicationState communicationState = GemCommunicationState.NotCommunicating;
    private GemControlState controlState = GemControlState.Offline;

    public event EventHandler<GemSessionStateChangedEventArgs>? StateChanged;

    public GemCommunicationState CommunicationState
    {
        get { lock (syncRoot) return communicationState; }
    }

    public GemControlState ControlState
    {
        get { lock (syncRoot) return controlState; }
    }

    public void MarkCommunicating(string reason = "S1F13/S1F14 completed")
        => Transition(GemCommunicationState.Communicating, null, reason);

    public void MarkCommunicationLost(string reason = "HSMS session is unavailable")
        => Transition(GemCommunicationState.NotCommunicating, GemControlState.Offline, reason);

    public void BeginAttemptOnline(string reason = "Online transition started")
    {
        if (CommunicationState != GemCommunicationState.Communicating)
            throw new InvalidOperationException("GEM communication must be established before going online.");

        Transition(null, GemControlState.AttemptOnline, reason);
    }

    public void CompleteOnline(bool remote, string reason = "Online transition completed")
    {
        if (CommunicationState != GemCommunicationState.Communicating)
            throw new InvalidOperationException("GEM communication must be established before going online.");
        if (ControlState != GemControlState.AttemptOnline)
            throw new InvalidOperationException("The GEM session is not attempting to go online.");

        Transition(null, remote ? GemControlState.OnlineRemote : GemControlState.OnlineLocal, reason);
    }

    public void RejectOnline(string reason = "Online transition rejected")
        => Transition(null, GemControlState.HostOffline, reason);

    public void AcceptHostOfflineRequest(string reason = "Host S1F15 Request OFF-LINE accepted")
    {
        if (CommunicationState != GemCommunicationState.Communicating)
            throw new InvalidOperationException("GEM communication must be established before accepting a Host OFF-LINE request.");

        // Equipment Offline 已经满足 Host 的下线请求，不应改写为 Host Offline。
        if (ControlState == GemControlState.Offline)
            return;

        Transition(null, GemControlState.HostOffline, reason);
    }

    public void SetOffline(string reason = "Offline requested")
        => Transition(null, GemControlState.Offline, reason);

    private void Transition(
        GemCommunicationState? nextCommunicationState,
        GemControlState? nextControlState,
        string reason)
    {
        GemSessionStateChangedEventArgs? args;
        lock (syncRoot)
        {
            GemCommunicationState previousCommunication = communicationState;
            GemControlState previousControl = controlState;
            GemCommunicationState nextCommunication = nextCommunicationState ?? previousCommunication;
            GemControlState nextControl = nextControlState ?? previousControl;

            if (previousCommunication == nextCommunication && previousControl == nextControl)
                return;

            communicationState = nextCommunication;
            controlState = nextControl;
            args = new(
                previousCommunication,
                nextCommunication,
                previousControl,
                nextControl,
                reason);
        }

        StateChanged?.Invoke(this, args);
    }
}
