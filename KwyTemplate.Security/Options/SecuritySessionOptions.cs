namespace KwyTemplate.Security.Options;

public sealed class SecuritySessionOptions
{
    /// <summary>
    /// 高级用户会话时长。小于等于 0 表示不自动回到操作员。
    /// </summary>
    public TimeSpan ElevatedUserSessionDuration { get; set; } = TimeSpan.FromMinutes(1);
}
