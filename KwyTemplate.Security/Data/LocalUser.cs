namespace KwyTemplate.Security.Data;

using KwyTemplate.Security.Identity;

public sealed class LocalUser
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public SecurityUserLevel Level { get; set; } = SecurityUserLevel.Operator;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
