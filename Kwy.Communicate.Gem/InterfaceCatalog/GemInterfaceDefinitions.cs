using Secs4Net;

namespace Kwy.Communicate.Gem;

/// <summary>指定 VID 在 GEM 接口中的用途。</summary>
/// <remarks>VID 可通过 S1F3/S1F4 查询，也可被 RPTID 引用并随 S6F11 事件上报。</remarks>
public enum GemVariableKind
{
    /// <summary>数据变量，用于表示事件或上下文数据，例如 Lot ID 或 Recipe ID。</summary>
    DataVariable,
    /// <summary>状态变量，例如当前设备状态、温度或压力。</summary>
    StatusVariable
}

public sealed record GemVariableDefinition(
    GemVid Vid, 
    string Name, 
    GemVariableKind Kind, 
    string? Unit = null,
    string? Description = null, 
    SecsFormat? Format = null);

public sealed record GemEquipmentConstantDefinition(
    GemEcid Ecid, 
    string Name, 
    string? Unit = null,
    string? Description = null, 
    SecsFormat? Format = null);

public sealed record GemReportDefinition(GemRptid Rptid, IReadOnlyList<GemVid> VariableIds);

public sealed record GemCollectionEventDefinition(
    GemCeid Ceid, 
    string Name, 
    IReadOnlyList<GemRptid> LinkedReports, 
    bool Enabled = true);

public sealed record GemAlarmDefinition(
    GemAlid Alid, 
    string Code, 
    string Text, 
    byte AlarmCode = 0, 
    bool Enabled = true);
