namespace KwyPecvd.RT.Runtime;

/// <summary>
/// Equipment Command查询结果的部署保留策略。
/// </summary>
public sealed class EquipmentCommandResultRetentionOptions
{
    public const string SectionName = "EquipmentCommandResultRetention";

    /// <summary>
    /// 终态结果保留时间。
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// 后台检查和清理间隔。
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(10);
}
