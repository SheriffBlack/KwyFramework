namespace Kwy.Communicate.Gem;

/// <summary>GEM 通信状态，与 HSMS 连接状态和 GEM Control State 分开管理。</summary>
public enum GemCommunicationState
{
    /// <summary>通信功能禁用：软件上关掉HSMS客户端，不尝试连接Host </summary>
    Disabled,
    /// <summary>通信已启用，但尚未完成 GEM Establish Communications。</summary>
    Enabled,
    /// <summary>当前未建立 GEM 通信，包括 HSMS 不可用或 S1F13/S1F14 尚未完成。</summary>
    NotCommunicating,
    /// <summary>HSMS 可用且 S1F13/S1F14 Establish Communications 已完成。</summary>
    Communicating
}

/// <summary>GEM Equipment 的控制状态。</summary>
/// <remarks>用于表示 Equipment 处于 Offline、Online Local 或 Online Remote，不等于设备的 Idle/Run/Fault 工艺状态。</remarks>
public enum GemControlState
{
    /// <summary>离线：设备不接受Host任何远程命令，Host无法控制设备；设备自主运行 </summary>
    Offline,
    /// <summary>正在尝试进入 Online，例如等待 S1F1/S1F2 确认或处理 Host S1F17 请求。</summary>
    AttemptOnline,
    /// <summary>Host 未接受 Online 转换，但 GEM 通信仍可用。</summary>
    HostOffline,
    /// <summary>Online-本地模式：Host和设备GEM链路Online，控制权在设备本地面板 </summary>
    OnlineLocal,
    /// <summary>Online-远程模式：Online状态，控制权交给Host，Host可以下发Start/Stop、远程工艺命令 </summary>
    OnlineRemote
}

/// <summary>GEM 通信端点的角色。</summary>
public enum GemHostRole
{
    /// <summary>Equipment 设备端。</summary>
    Equipment,
    /// <summary>Fab EAP/Host 端。</summary>
    Host
}
