namespace Kwy.Device.Abstractions.Equipment;

/// <summary>
/// 指定整机事件的类型。
/// </summary>
public enum EquipmentEventKind
{
    /// <summary>一般信息。</summary>
    Information,
    /// <summary>整机状态发生变化。</summary>
    StateChange,
    /// <summary>生产或工艺过程事件。</summary>
    Process,
    /// <summary>物料相关事件。</summary>
    Material,
    /// <summary>设备报警事件。</summary>
    Alarm,
    /// <summary>审计事件。</summary>
    Audit
}

/// <summary>
/// 指定整机事件的严重程度。
/// </summary>
public enum EquipmentEventSeverity
{
    /// <summary>跟踪信息。</summary>
    Trace,
    /// <summary>一般信息。</summary>
    Information,
    /// <summary>警告。</summary>
    Warning,
    /// <summary>错误。</summary>
    Error,
    /// <summary>严重错误。</summary>
    Critical
}

/// <summary>
/// 表示由整机运行过程产生的领域事件。
/// </summary>
/// <param name="Code">稳定且可识别的事件代码。</param>
/// <param name="Message">事件说明。</param>
/// <param name="Kind">事件类型。</param>
/// <param name="Severity">事件严重程度。</param>
/// <param name="Source">事件来源，可为空。</param>
/// <param name="Timestamp">事件发生时间；为空时可由接收方补充。</param>
/// <param name="Properties">事件的扩展属性，可为空。</param>
public sealed record EquipmentEvent(
    string Code,
    string Message,
    EquipmentEventKind Kind = EquipmentEventKind.Information,
    EquipmentEventSeverity Severity = EquipmentEventSeverity.Information,
    string? Source = null,
    DateTimeOffset? Timestamp = null,
    IReadOnlyDictionary<string, string>? Properties = null);
