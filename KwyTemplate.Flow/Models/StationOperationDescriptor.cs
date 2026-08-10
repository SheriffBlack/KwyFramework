namespace KwyTemplate.Flow.Models;

/// <summary>
/// 工位人工操作描述。当前模板只保留点检和校正两个标准操作。
/// </summary>
public sealed class StationOperationDescriptor
{
    public const string Check = "Check";
    public const string Calibration = "Calibration";

    /// <summary>
    /// 操作编码，例如 <see cref="Check" /> 或 <see cref="Calibration" />。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// UI 显示名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
