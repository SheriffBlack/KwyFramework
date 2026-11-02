using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>
/// GEM Stream 7 中传输的 PPID 与 PPBODY。
/// </summary>
/// <param name="Ppid"></param>
/// <param name="Body"></param>
/// <param name="Version"></param>
/// <param name="UpdatedAt"></param>
/// <remarks>
/// 实例拥有 <paramref name="Body"/> 的生命周期，使用完成后应释放。
/// Repository 若需长期保存，必须复制或序列化 PPBODY，不能保留该实例中的 Item 引用。
/// </remarks>
public sealed record GemProcessProgram(
    string Ppid,
    Item Body,
    string? Version = null,
    DateTimeOffset? UpdatedAt = null) : IDisposable
{
    public void Dispose() => Body.Dispose();
}

/// <summary>保存配方回执 </summary>
/// <remarks>Host 通过 S7F3 上传 Process Program，设备存储边界的返回结果。</remarks>
public enum GemProcessProgramSaveStatus
{
    /// <summary>保存成功 </summary>
    Saved,
    /// <summary>同名配方已经存在 </summary>
    AlreadyExists,
    /// <summary>拒绝保存（校验失败、权限限制） </summary>
    Rejected
}

/// <summary>删除配方回执 </summary>
/// <remarks>Host 通过 S7F17 请求删除 Process Program 时的返回结果。</remarks>
public enum GemProcessProgramDeleteStatus
{
    /// <summary>删除成功 </summary>
    Deleted,
    /// <summary>PP名字不存在 </summary>
    NotFound,
    /// <summary>拒绝删除（正在Active运行中的配方不能删） </summary>
    Rejected
}

public sealed record GemProcessProgramSaveOptions(bool Overwrite = false);

public sealed record GemProcessProgramSaveResult(
    GemProcessProgramSaveStatus Status,
    string? Message = null)
{
    public bool Succeeded => Status == GemProcessProgramSaveStatus.Saved;
}

public sealed record GemProcessProgramDeleteResult(
    GemProcessProgramDeleteStatus Status,
    string? Message = null)
{
    public bool Succeeded => Status == GemProcessProgramDeleteStatus.Deleted;
}

/// <summary>Stream 7 的 ACKC7 Process Program 应答码。</summary>
public enum GemProcessProgramAcknowledgeCode : byte
{
    /// <summary>请求已接受。</summary>
    Accepted = 0,
    /// <summary>当前不允许执行请求。</summary>
    PermissionNotGranted = 1,
    /// <summary>Process Program 长度错误。</summary>
    LengthError = 2,
    /// <summary>数据结构超出容量。</summary>
    MatrixOverflow = 3,
    /// <summary>PPID 不存在。</summary>
    PpidNotFound = 4,
    /// <summary>当前模式不支持该请求。</summary>
    ModeUnsupported = 5,
    /// <summary>命令已受理，完成结果稍后上报。</summary>
    AcceptedForAsynchronousCompletion = 6
}

/// <summary>S7F2 PPGRANT 标准结果。</summary>
public enum GemProcessProgramLoadGrant : byte
{
    /// <summary>允许上传，Host 可以继续发送 S7F3。</summary>
    Granted = 0,
    /// <summary>设备本地已经存在同名PPID配方，拒绝本次上传请求 </summary>
    AlreadyHave = 1,
    /// <summary>设备配方存储空间已满，放不下新PP </summary>
    NoSpace = 2,
    /// <summary>PPID非法：名字含非法字符、长度超限、格式不符合设备规范 </summary>
    InvalidPpid = 3,
    /// <summary>设备忙，当前不能接收 Process Program。</summary>
    Busy = 4,
    /// <summary>拒绝上传，且不属于上述明确原因。</summary>
    WillNotAccept = 5
}

/// <summary>S7F1 Process Program Load Inquire 的业务请求。</summary>
public sealed record GemProcessProgramLoadInquire(string Ppid, uint Length);
