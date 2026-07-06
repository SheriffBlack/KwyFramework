namespace KwyTemplate.Security.Identity;

public sealed record CurrentUser(
    long Id,
    string UserName,
    string DisplayName,
    SecurityUserLevel Level)
{
    public bool HasLevel(SecurityUserLevel requiredLevel) => Level >= requiredLevel;
}
