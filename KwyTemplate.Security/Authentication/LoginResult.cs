using KwyTemplate.Security.Identity;

namespace KwyTemplate.Security.Authentication;

public sealed record LoginResult(
    bool Succeeded,
    CurrentUser? User = null,
    string? ErrorMessage = null)
{
    public static LoginResult Success(CurrentUser user) => new(true, user);

    public static LoginResult Failed(string message) => new(false, ErrorMessage: message);
}

