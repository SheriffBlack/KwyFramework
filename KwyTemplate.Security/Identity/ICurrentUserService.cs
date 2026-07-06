namespace KwyTemplate.Security.Identity;

public interface ICurrentUserService
{
    CurrentUser? CurrentUser { get; }

    bool IsAuthenticated { get; }

    event EventHandler<CurrentUser?>? CurrentUserChanged;

    void SignIn(CurrentUser user);

    void SignOut();
}

