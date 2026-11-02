namespace Kwy.Communicate.Gem;

/// <summary>Collection Event ID 事件ID </summary>
public readonly record struct GemCeid(uint Value);

/// <summary>RPTID：Report ID 报表ID </summary>
public readonly record struct GemRptid(uint Value);

/// <summary>VID：Variable ID 变量ID </summary>
public readonly record struct GemVid(uint Value);

/// <summary>ALID：Alarm ID 报警ID </summary>
public readonly record struct GemAlid(uint Value);

/// <summary>ECID：Equipment Constant ID 设备常量ID </summary>
/// <remarks>属于设备的固有配置、硬件标定、出厂参数 </remarks>
public readonly record struct GemEcid(uint Value);
