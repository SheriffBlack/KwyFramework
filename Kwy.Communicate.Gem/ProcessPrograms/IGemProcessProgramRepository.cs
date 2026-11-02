namespace Kwy.Communicate.Gem;

/// <summary>定义 GEM Stream 7 Process Program 与设备配方系统之间的存储边界。</summary>
/// <remarks>
/// 本接口处理 PPID/PPBODY，不表示通信库拥有设备完整的 Recipe 领域模型。
/// 生产项目应由 Recipe Service 实现此接口，并在保存和删除时完成版本、审批、重名、容量和当前设备状态等业务判定。保存成功仅表示已持久化，不表示已选择或执行配方。
/// </remarks>
public interface IGemProcessProgramRepository
{
    Task<IReadOnlyList<string>> ListPpidsAsync(CancellationToken cancellationToken = default);

    /// <summary>读取 Process Program；返回对象的所有权转移给调用方。</summary>
    Task<GemProcessProgram?> FindAsync(string ppid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存 Process Program。实现若需在方法返回后继续使用 PPBODY，必须复制或序列化。
    /// </summary>
    Task<GemProcessProgramSaveResult> SaveAsync(
        GemProcessProgram processProgram,
        GemProcessProgramSaveOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<GemProcessProgramDeleteResult> DeleteAsync(
        string ppid,
        CancellationToken cancellationToken = default);
}
